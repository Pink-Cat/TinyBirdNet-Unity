﻿using UnityEngine;

namespace TinyBirdNet {

	// This class represents the player entity in a network game, there can be multiple players per client.
	// when there are multiple people playing on one machine.
	// The server has one TinyNetConnection per peer.
	public class TinyNetPlayerController {

		public short playerControllerId = -1;
		//public TinyNetIdentity tinyNetId;
		//public GameObject gameObject;

		public TinyNetPlayerController() {
		}

		public bool IsValid { get { return playerControllerId != -1; } }

		/*public TinyNetPlayerController(GameObject go, short playerControllerId) {
			gameObject = go;
			tinyNetId = go.GetComponent<TinyNetIdentity>();

			this.playerControllerId = playerControllerId;
		}*/

		public TinyNetPlayerController(short playerControllerId) {
			this.playerControllerId = playerControllerId;
		}

		public override string ToString() {
			//return string.Format("ID={0} NetworkIdentity NetID={1} Player={2}", new object[] { playerControllerId, (tinyNetId != null ? tinyNetId.NetworkID.ToString() : "null"), (gameObject != null ? gameObject.name : "null") });
			return string.Format("PlayerID={" + playerControllerId + "}");
		}
	}
}
