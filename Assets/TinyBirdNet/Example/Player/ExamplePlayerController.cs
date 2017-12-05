﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TinyBirdNet;
using TinyBirdNet.Messaging;
using LiteNetLib.Utils;

public class ExamplePlayerController : TinyNetPlayerController {

	//<summary>Will send the input buffer every X fixed frames</summary>*/
	//public int inputBufferSize = 5;

	//protected List<ExampleInputMessage> inputBuffer;
	protected ExampleInputMessage inputMessageReader = new ExampleInputMessage();
	protected ExampleInputMessage inputMessageBuffer = new ExampleInputMessage();

	protected Coroutine fixedUpdateCoroutine;

	protected ExamplePawn pawn;

	public ExamplePawn Pawn { get { return pawn; } }

	protected float timeForSpawn;

	protected bool bAskedForPawn = false;

	public ExamplePlayerController() : base() {
		/*inputBuffer = new List<ExampleInputMessage>(inputBufferSize);
		inputMessages = new ExampleInputMessage[inputBufferSize];

		for (int i = 0; i < inputMessages.Length; i++) {
			inputMessages[i] = new ExampleInputMessage();
		}*/
	}

	public ExamplePlayerController(short playerControllerId) : base(playerControllerId) {
		//Hacky way, but I want a coroutine...
		//if (isaclientandnotaserver)
		fixedUpdateCoroutine = TinyNetGameManager.instance.StartCoroutine(FixedUpdateLoop());

		inputMessageBuffer.playerControllerId = playerControllerId;
	}

	//Finalizer
	~ExamplePlayerController() {
		if (fixedUpdateCoroutine != null) {
			TinyNetGameManager.instance.StopCoroutine(fixedUpdateCoroutine);
		}
	}

	public static byte VectorToDirection(Vector2 axis) {
		int type = ((Mathf.RoundToInt(Mathf.Atan2(axis.y, axis.x) / (2f * Mathf.PI / 4f))) + 4) % 4;

		//0 = right, 3 = down
		switch (type) {
			case 0:
				return 2;
			case 1:
				return 1;
			case 2:
				return 4;
			case 3:
				return 3;
		}

		return 0;
	}

	IEnumerator FixedUpdateLoop() {
		while (true) {
			Vector2 axis = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")).normalized;

			MoveToDir(axis == Vector2.zero ? (byte)0 : VectorToDirection(axis));

			if (Input.GetButton("Fire1")) {
				Shoot();
			}

			if (pawn != null) {
				inputMessageBuffer.xPos = pawn.transform.position.x;
				inputMessageBuffer.zPos = pawn.transform.position.z;

				if (!TinyNetGameManager.instance.isServer) {
					conn.Send(inputMessageBuffer, LiteNetLib.SendOptions.Sequenced);
				}
			}

			yield return new WaitForFixedUpdate();
		}
	}

	public override void GetInputMessage(TinyNetMessageReader netMsg) {
		netMsg.ReadMessage(inputMessageReader);

		if (pawn != null) {
			pawn.ServerSyncPosFromOwner(inputMessageReader.xPos, inputMessageReader.zPos);
		}
	}

	protected byte MoveToDir(byte direction) {
		if (pawn != null) {
			pawn.MoveToDir(direction);
		}

		return direction;
	}

	protected void Shoot() {
		if (pawn != null) {
			pawn.Shoot();
		} else if (!bAskedForPawn && timeForSpawn <= Time.time) {
			TinyNetShortMessage spawnPawnMsg = new TinyNetShortMessage();
			spawnPawnMsg.msgType = TinyNetMsgType.SpawnPlayer;
			spawnPawnMsg.value = playerControllerId;

			conn.Send(spawnPawnMsg, LiteNetLib.SendOptions.ReliableOrdered);

			bAskedForPawn = true;
		}
	}

	public void GetPawn(ExamplePawn nPawn) {
		pawn = nPawn;
	}

	public void LosePawn() {
		pawn = null;
		bAskedForPawn = false;

		timeForSpawn = Time.time + 3f;
	}
}

public class ExampleInputMessage : TinyNetInputMessage {
	public float xPos;
	public float zPos;

	public override void Deserialize(NetDataReader reader) {
		base.Deserialize(reader);

		xPos = reader.GetFloat();
		zPos = reader.GetFloat();
	}

	public override void Serialize(NetDataWriter writer) {
		base.Serialize(writer);

		writer.Put(xPos);
		writer.Put(zPos);
	}
}