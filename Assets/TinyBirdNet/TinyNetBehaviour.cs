﻿using LiteNetLib.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TinyBirdNet.Utils;
using TinyBirdUtils;
using UnityEngine;

namespace TinyBirdNet {

	/// <summary>
	/// A TinyNetBehaviour is a MonoBehaviour who implements the interface ITinyNetComponent and comes with a bunch of useful stuff.
	/// </summary>
	/// <seealso cref="UnityEngine.MonoBehaviour" />
	/// <seealso cref="TinyBirdNet.ITinyNetComponent" />
	/// <seealso cref="TinyBirdNet.ITinyNetInstanceID" />
	[RequireComponent(typeof(TinyNetIdentity))]
	public class TinyNetBehaviour : MonoBehaviour, ITinyNetComponent {

		/// <summary>
		/// A static NetDataWriter that can be used to convert most Objects to bytes.
		/// </summary>
		protected static NetDataWriter rpcRecycleWriter = new NetDataWriter();

		/// <summary>
		/// The ID of an instance in the network, given by the server on spawn.
		/// </summary>
		public TinyNetworkID TinyInstanceID { get; protected set; }

		private Dictionary<string, TinyNetPropertyAccessor<byte>> byteAccessor = new Dictionary<string, TinyNetPropertyAccessor<byte>>();
		private Dictionary<string, TinyNetPropertyAccessor<sbyte>> sbyteAccessor = new Dictionary<string, TinyNetPropertyAccessor<sbyte>>();
		private Dictionary<string, TinyNetPropertyAccessor<short>> shortAccessor = new Dictionary<string, TinyNetPropertyAccessor<short>>();
		private Dictionary<string, TinyNetPropertyAccessor<ushort>> ushortAccessor = new Dictionary<string, TinyNetPropertyAccessor<ushort>>();
		private Dictionary<string, TinyNetPropertyAccessor<int>> intAccessor = new Dictionary<string, TinyNetPropertyAccessor<int>>();
		private Dictionary<string, TinyNetPropertyAccessor<uint>> uintAccessor = new Dictionary<string, TinyNetPropertyAccessor<uint>>();
		private Dictionary<string, TinyNetPropertyAccessor<long>> longAccessor = new Dictionary<string, TinyNetPropertyAccessor<long>>();
		private Dictionary<string, TinyNetPropertyAccessor<ulong>> ulongAccessor = new Dictionary<string, TinyNetPropertyAccessor<ulong>>();
		private Dictionary<string, TinyNetPropertyAccessor<float>> floatAccessor = new Dictionary<string, TinyNetPropertyAccessor<float>>();
		private Dictionary<string, TinyNetPropertyAccessor<double>> doubleAccessor = new Dictionary<string, TinyNetPropertyAccessor<double>>();
		private Dictionary<string, TinyNetPropertyAccessor<bool>> boolAccessor = new Dictionary<string, TinyNetPropertyAccessor<bool>>();
		private Dictionary<string, TinyNetPropertyAccessor<string>> stringAccessor = new Dictionary<string, TinyNetPropertyAccessor<string>>();

		private RPCDelegate[] rpcHandlers;

		private string[] propertiesName;
		private Type[] propertiesTypes;

		/// <summary>
		/// Gets or sets a value indicating whether this instance is dirty.
		/// </summary>
		/// <value>
		///   <c>true</c> if instance is dirty; otherwise, <c>false</c>.
		/// </value>
		public bool IsDirty { get; protected set; }

		/// <summary>
		/// The dirty flag is a BitArray of size 32 that represents if a TinyNetSyncVar is dirty.
		/// </summary>
		private BitArray _dirtyFlag;

		/// <summary>
		/// [Server Only] The last Time.time registered at an UpdateDirtyFlag call.
		/// </summary>
		protected float _lastSendTime;

		/// <summary>
		/// Gets a value indicating whether this instance is server.
		/// </summary>
		/// <value>
		///   <c>true</c> if this instance is server; otherwise, <c>false</c>.
		/// </value>
		public bool isServer { get { return TinyNetGameManager.instance.isServer; } }
		/// <summary>
		/// Gets a value indicating whether this instance is client.
		/// </summary>
		/// <value>
		///   <c>true</c> if this instance is client; otherwise, <c>false</c>.
		/// </value>
		public bool isClient { get { return TinyNetGameManager.instance.isClient; } }
		

		public bool hasAuthority { get { return NetIdentity.HasAuthority; } }

		/// <summary>
		/// The net identity in this GameObject.
		/// </summary>
		TinyNetIdentity _netIdentity;
		/// <summary>
		/// Gets the net identity.
		/// </summary>
		/// <value>
		/// The net identity.
		/// </value>
		public TinyNetIdentity NetIdentity {
			get {
				if (_netIdentity == null) {
					_netIdentity = GetComponent<TinyNetIdentity>();

					if (_netIdentity == null) {
						if (TinyNetLogLevel.logError) { TinyLogger.LogError("There is no TinyNetIdentity on this object. Please add one."); }
					}

					return _netIdentity;
				}

				return _netIdentity;
			}
		}

		/// <summary>
		/// Receives the network identifier.
		/// </summary>
		/// <param name="newID">The new identifier.</param>
		public void ReceiveNetworkID(TinyNetworkID newID) {
			TinyInstanceID = newID;
		}

		protected virtual void Awake() {
			NetIdentity.RegisterEventHandler(TinyNetIdentity.TinyNetComponentEvents.OnNetworkCreate, OnNetworkCreate);
		}

		protected virtual void CreateDirtyFlag() {
			int numberOfSyncVars = TinyNetStateSyncer.GetNumberOfSyncedProperties(GetType());

			if (numberOfSyncVars == 0) {
				_dirtyFlag = null;
			} else if (numberOfSyncVars <= 8) {
				_dirtyFlag = new BitArray(8);
			} else if (numberOfSyncVars <= 16) {
				_dirtyFlag = new BitArray(16);
			} else if (numberOfSyncVars <= 32) {
				_dirtyFlag = new BitArray(32);
			} else if (numberOfSyncVars <= 64) {
				_dirtyFlag = new BitArray(64);
			} else {
				if (TinyNetLogLevel.logError) { TinyLogger.LogError("TinyNetBehaviour::OnNetworkCreate amount of TinyNetSyncVar is bigger than 64."); }
				return;
			}
		}

		/// <summary>
		/// Sets the bit value on the dirty flag at the given index.
		/// </summary>
		/// <param name="index">The index.</param>
		/// <param name="bValue">The new bool value.</param>
		public void SetDirtyFlag(int index, bool bValue) {
			_dirtyFlag[index] = bValue;

			IsDirty = false;
			for (int i = 0; i < _dirtyFlag.Length; i++) {
				if (_dirtyFlag[i]) {
					IsDirty = true;
				}
			}
		}

		/// <summary>
		/// Updates the dirty flag.
		/// <para>This will only set dirty values to true, it will not revert them to false.</para>
		/// </summary>
		private void UpdateDirtyFlag() {
			TinyNetStateSyncer.UpdateDirtyFlagOf(this, _dirtyFlag);

			IsDirty = false;
			for (int i = 0; i < _dirtyFlag.Length; i++) {
				if (_dirtyFlag[i]) {
					IsDirty = true;
				}
			}

			_lastSendTime = Time.time;
		}

		/// <summary>
		/// Sets all the dirty flags and IsDirty to <c>false</c>.
		/// </summary>
		private void ClearDirtyFlag() {
			IsDirty = false;

			for (int i = 0; i < _dirtyFlag.Length; i++) {
				_dirtyFlag[i] = false;
			}
		}

		/// <summary>
		/// Creates the TinyNetSyncVar property accessors.
		/// </summary>
		public void CreateAccessors() {
			Type type;

			for (int i = 0; i < propertiesName.Length; i++) {
				type = propertiesTypes[i];

				if (type == typeof(byte)) {
					byteAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<byte>(this, propertiesName[i]));
				} else if (type == typeof(sbyte)) {
					sbyteAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<sbyte>(this, propertiesName[i]));
				} else if (type == typeof(short)) {
					shortAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<short>(this, propertiesName[i]));
				} else if (type == typeof(ushort)) {
					ushortAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<ushort>(this, propertiesName[i]));
				} else if (type == typeof(int)) {
					intAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<int>(this, propertiesName[i]));
				} else if (type == typeof(uint)) {
					uintAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<uint>(this, propertiesName[i]));
				} else if (type == typeof(long)) {
					longAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<long>(this, propertiesName[i]));
				} else if (type == typeof(ulong)) {
					ulongAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<ulong>(this, propertiesName[i]));
				} else if (type == typeof(float)) {
					floatAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<float>(this, propertiesName[i]));
				} else if (type == typeof(double)) {
					doubleAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<double>(this, propertiesName[i]));
				} else if (type == typeof(bool)) {
					boolAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<bool>(this, propertiesName[i]));
				} else if (type == typeof(string)) {
					stringAccessor.Add(propertiesName[i], new TinyNetPropertyAccessor<string>(this, propertiesName[i]));
				}
			}
		}

		/// <summary>
		/// Checks if a TinyNetSyncVar property updated.
		/// </summary>
		/// <param name="propName">Name of the property.</param>
		/// <param name="type">The type.</param>
		/// <returns></returns>
		public bool CheckIfPropertyUpdated(string propName, Type type) {
			if (type == typeof(byte)) {
				return byteAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(sbyte)) {
				return sbyteAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(short)) {
				return shortAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(ushort)) {
				return ushortAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(int)) {
				return intAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(uint)) {
				return uintAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(long)) {
				return longAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(ulong)) {
				return ulongAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(float)) {
				return floatAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(double)) {
				return doubleAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(bool)) {
				return boolAccessor[propName].CheckIfChangedAndUpdate(this);
			} else if (type == typeof(string)) {
				return stringAccessor[propName].CheckIfChangedAndUpdate(this);
			}

			return false;
		}

		/// <summary>
		/// Serializates the data.
		/// </summary>
		/// <param name="writer">The writer.</param>
		/// <param name="firstStateUpdate">if set to <c>true</c> it's the first state update.</param>
		public virtual void TinySerialize(NetDataWriter writer, bool firstStateUpdate) {
			if (!firstStateUpdate) {
				//writer.Put((uint)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag));
				switch (_dirtyFlag.Length) {
					case 0:
						// Do nothing!
						break;
					case 8:
						//byte test = (byte)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag);
						//Debug.Log("Serialize DirtyFlag: " + test);
						writer.Put((byte)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag));
						break;
					case 16:
						writer.Put((ushort)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag));
						break;
					case 32:
						writer.Put((uint)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag));
						break;
					case 64:
						writer.Put((ulong)TinyBitArrayUtil.BitArrayToU64(_dirtyFlag));
						break;
				}
			}

			//Debug.Log("DirtyFlag: " + _dirtyFlag[0]);
			//Debug.Log("DirtyFlag: " + TinyBitArrayUtil.Display(_dirtyFlag));

			Type type;
			int maxSyncVar = propertiesName.Length;

			for (int i = 0; i < maxSyncVar; i++) {
				if (!firstStateUpdate && _dirtyFlag[i] == false) {
					continue;
				}

				type = propertiesTypes[i];

				if (type == typeof(byte)) {
					writer.Put(byteAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(sbyte)) {
					writer.Put(sbyteAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(short)) {
					writer.Put(shortAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(ushort)) {
					writer.Put(ushortAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(int)) {
					writer.Put(intAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(uint)) {
					writer.Put(uintAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(long)) {
					writer.Put(longAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(ulong)) {
					writer.Put(ulongAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(float)) {
					writer.Put(floatAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(double)) {
					writer.Put(doubleAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(bool)) {
					writer.Put(boolAccessor[propertiesName[i]].Get(this));
				} else if (type == typeof(string)) {
					writer.Put(stringAccessor[propertiesName[i]].Get(this));
				}
			}
		}

		/// <inheritdoc />
		public virtual void TinyDeserialize(TinyNetStateReader reader, bool firstStateUpdate) {
			if (!firstStateUpdate) {
				//uint dFlag = reader.GetUInt();
				//TinyBitArrayUtil.U64ToBitArray(dFlag, _dirtyFlag);
				switch (_dirtyFlag.Length) {
					case 0:
						break;
					case 8:
						//Debug.Log("Deserialize DirtyFlag: " + reader.PeekByte());
						TinyBitArrayUtil.U64ToBitArray(reader.GetByte(), _dirtyFlag);
						break;
					case 16:
						TinyBitArrayUtil.U64ToBitArray(reader.GetUShort(), _dirtyFlag);
						break;
					case 32:
						TinyBitArrayUtil.U64ToBitArray(reader.GetUInt(), _dirtyFlag);
						break;
					case 64:
						TinyBitArrayUtil.U64ToBitArray(reader.GetULong(), _dirtyFlag);
						break;
				}
			}

			//Debug.Log("DirtyFlag: " + _dirtyFlag[0]);
			//Debug.Log("DirtyFlag: " + TinyBitArrayUtil.Display(_dirtyFlag));

			Type type;
			int maxSyncVar = propertiesName.Length;

			for (int i = 0; i < maxSyncVar; i++) {
				if (!firstStateUpdate && _dirtyFlag[i] == false) {
					continue;
				}

				type = propertiesTypes[i];

				if (type == typeof(byte)) {
					byteAccessor[propertiesName[i]].Set(this, reader.GetByte());
				} else if (type == typeof(sbyte)) {
					sbyteAccessor[propertiesName[i]].Set(this, reader.GetSByte());
				} else if (type == typeof(short)) {
					shortAccessor[propertiesName[i]].Set(this, reader.GetShort());
				} else if (type == typeof(ushort)) {
					ushortAccessor[propertiesName[i]].Set(this, reader.GetUShort());
				} else if (type == typeof(int)) {
					intAccessor[propertiesName[i]].Set(this, reader.GetInt());
				} else if (type == typeof(uint)) {
					uintAccessor[propertiesName[i]].Set(this, reader.GetUInt());
				} else if (type == typeof(long)) {
					longAccessor[propertiesName[i]].Set(this, reader.GetLong());
				} else if (type == typeof(ulong)) {
					ulongAccessor[propertiesName[i]].Set(this, reader.GetULong());
				} else if (type == typeof(float)) {
					floatAccessor[propertiesName[i]].Set(this, reader.GetFloat());
				} else if (type == typeof(double)) {
					doubleAccessor[propertiesName[i]].Set(this, reader.GetDouble());
				} else if (type == typeof(bool)) {
					boolAccessor[propertiesName[i]].Set(this, reader.GetBool());
				} else if (type == typeof(string)) {
					stringAccessor[propertiesName[i]].Set(this, reader.GetString());
				}
			}
		}

		/// <summary>
		/// Registers the RPC delegate.
		/// </summary>
		/// <param name="rpcDel">The RPC delegate.</param>
		/// <param name="methodName">Name of the method.</param>
		protected void RegisterRPCDelegate(RPCDelegate rpcDel, string methodName) {
			if (rpcHandlers == null) {
				rpcHandlers = new RPCDelegate[TinyNetStateSyncer.GetNumberOfRPCMethods(GetType())];
			}

			int index = TinyNetStateSyncer.GetRPCMethodIndexFromType(GetType(), methodName);
			rpcHandlers[index] = new RPCDelegate(rpcDel);
		}

		/// <summary>
		/// Sends the RPC.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="rpcName">Name of the RPC.</param>
		public virtual void SendRPC(NetDataWriter stream, string rpcName) {
			RPCMethodInfo rpcMethodInfo = null;
			int rpcMethodIndex = TinyNetStateSyncer.GetRPCMethodInfoFromType(GetType(), rpcName, ref rpcMethodInfo);

			SendRPC(stream, rpcMethodInfo.target, rpcMethodInfo.caller, rpcMethodIndex);
		}

		/// <summary>
		/// Sends the RPC.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="target">The target.</param>
		/// <param name="caller">The caller.</param>
		/// <param name="rpcMethodIndex">Index of the RPC method.</param>
		public virtual void SendRPC(NetDataWriter stream, RPCTarget target, RPCCallers caller, int rpcMethodIndex) {
			if (target == RPCTarget.ClientOwner) {
				if (!isServer || _netIdentity.HasAuthority) {
					//We are not the server or we are the owner, so we can't or have no need to replicate
					return;
				}
			} else if (target == RPCTarget.Server) {
				if (isServer) {
					//We are the server, no need to replicate
					return;
				}
			} else if (target == RPCTarget.Everyone) {
				if (!isServer) {
					//We are not the server, we don't have a connection to everyone
					return;
				}
			}

			switch(target) {
				case RPCTarget.Server:
					TinyNetClient.instance.SendRPCToServer(stream, rpcMethodIndex, this);
					return;
				case RPCTarget.ClientOwner:
					TinyNetServer.instance.SendRPCToClientOwner(stream, rpcMethodIndex, this);
					return;
				case RPCTarget.Everyone:
					TinyNetServer.instance.SendRPCToAllClients(stream, rpcMethodIndex, this);
					return;
			}
		}

		/// <summary>
		/// Invokes the RPC.
		/// </summary>
		/// <param name="rpcMethodIndex">Index of the RPC method.</param>
		/// <param name="reader">The reader.</param>
		/// <returns></returns>
		public virtual bool InvokeRPC(int rpcMethodIndex, TinyNetStateReader reader) {
			if (rpcHandlers[rpcMethodIndex] == null) {
				if (TinyNetLogLevel.logError) { TinyLogger.LogError("TinyNetBehaviour::InvokeRPC netId:" + TinyInstanceID + " RPCDelegate is not registered."); }
				return false;
			}

			rpcHandlers[rpcMethodIndex].Invoke(reader);

			return true;
		}

		/// <summary>
		/// [Server Only] Called every server update, after all FixedUpdates.
		/// <para> It is used to check if it is time to send the current state to clients. </para>
		/// </summary>
		public virtual void TinyNetUpdate() {
			ClearDirtyFlag();
			UpdateDirtyFlag();
		}

		/// <summary>
		/// Remember that this is called first and before variables are synced.
		/// </summary>
		public virtual void OnNetworkCreate() {
			TinyNetStateSyncer.OutPropertyNamesFromType(GetType(), out propertiesName);
			TinyNetStateSyncer.OutPropertyTypesFromType(GetType(), out propertiesTypes);

			CreateAccessors();

			CreateDirtyFlag();

			if (isServer) {
				UpdateDirtyFlag();
			}
		}

		public virtual void OnNetworkDestroy() {
		}

		public virtual void OnStartServer() {
		}

		public virtual void OnStartClient() {
		}

		public virtual void OnStateUpdate() {
		}

		public virtual void OnStartAuthority() {
		}

		public virtual void OnStopAuthority() {
		}

		public virtual void OnGiveAuthority() {
		}

		public virtual void OnRemoveAuthority() {
		}

		public virtual void OnSetLocalVisibility(bool vis) {
		}
	}
}
