﻿using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using System.Collections.Generic;
using System.Linq;

namespace SyncTransformSystem {	
	[RequireComponent(typeof(Animator))]
	[NetworkSettings(channel=1)]
	public class SyncAnimation : NetworkBehaviour {
		[SyncVar]
		public float interval = 0.1f;
		[SyncVar]
		public float latency = 2f;

		protected Animator _animator;
		protected Transform[] _bones;
		protected float _nextUpdateTime;

		protected SyncVector3List _syncPositionList;
		protected SyncQuaternionList _syncRotationList;
		protected SyncVector3List _syncScaleList;

		Skelton _tmpdata0, _tmpdata1;
		List<Skelton> _datastream;
		int _updateCount;

		public override float GetNetworkSendInterval () { return interval; }
		public float Time { get { return TimeProvider.Instance.Time; } }

		void Awake () {
			_animator = GetComponent<Animator> ();
			_animator.enabled = false;

			_nextUpdateTime = Time;
			_bones = BoneList (transform);

			_syncPositionList = new SyncVector3List ();
			_syncRotationList = new SyncQuaternionList ();
			_syncScaleList = new SyncVector3List ();
			_datastream = new List<Skelton> ();
			_tmpdata0 = new Skelton(){ bones = new Bone[_bones.Length] };
			_tmpdata1 = new Skelton (){ bones = new Bone[_bones.Length] };
		}
		void Update() {
			if (isServer) {
				var t = Time;
				if (_nextUpdateTime <= t) {
					_nextUpdateTime += GetNetworkSendInterval ();
					NotifyData ();
				}
			} else if (isClient) {
				ApplyData();
			}
		}

		void InitData (Skelton sk) {
			_syncPositionList.Clear ();
			_syncRotationList.Clear ();
			_syncScaleList.Clear ();
            for (var i = 0; i < sk.bones.Length; i++)
				AddBoneOnData(sk.bones [i]);
		}
		Bone CreateBoneFromDataAt (int i) {
			return new Bone () {
				position = _syncPositionList [i],
				rotation = _syncRotationList [i],
				scale = _syncScaleList [i]
			};
		}
		Skelton CreateSkeltonFromData (float time) {
			var boneCount = _syncPositionList.Count;
			var bones = new Bone[boneCount];
			for (var i = 0; i < boneCount; i++)
				bones [i] = CreateBoneFromDataAt (i);
			var sk = new Skelton () {
				time = time,
				bones = bones
			};
			return sk;
		}
		void SwapTemp () {
			var tmp = _tmpdata0;
			_tmpdata0 = _tmpdata1;
			_tmpdata1 = tmp;
		}

		void AddBoneOnData(Bone bone) {
			_syncPositionList.Add (bone.position);
			_syncRotationList.Add (bone.rotation);
			_syncScaleList.Add (bone.scale);
		}
		void SaveDataChange () {
			var boneCount = _syncPositionList.Count;
			for (var i = 0; i < boneCount; i++)
				SaveDataChangeAt (i, _tmpdata0.bones [i], _tmpdata1.bones [i]);
		}
		void SaveDataChangeAt(int i, Bone prev, Bone next) {
            var change = next.Changed (prev);
            if ((change & Bone.ChangeFlags.Position) != 0)
				_syncPositionList [i] = next.position;
            if ((change & Bone.ChangeFlags.Rotation) != 0)
				_syncRotationList [i] = next.rotation;
            if ((change & Bone.ChangeFlags.Scale) != 0)
				_syncScaleList [i] = next.scale;
		}

		public class SyncVector3List : SyncListStruct<Vector3> {}
		public class SyncQuaternionList : SyncListStruct<Quaternion> {}

		#region Bones
		public static IEnumerable<Transform> Listup(Transform root, bool includingRoot = true) {
			if (includingRoot)
				yield return root;
			for (var i = 0; i < root.childCount; i++)
				foreach (var tr in Listup (root.GetChild (i)))
					yield return tr;
		}
		public static Transform[] BoneList(Transform root, bool includingRoot = true) {
			return Listup (root, includingRoot).ToArray ();
		}
		#endregion

		#region Server
		public override void OnStartServer () {
			_animator.enabled = true;
			_tmpdata0.Save (Time, _bones);
			InitData(_tmpdata0);
		}
		void NotifyData () {
			_tmpdata1.Save (Time, _bones);
			SaveDataChange();
			SwapTemp();
		}
		#endregion

		#region Client
		public override void OnStartClient () {
			_datastream.Add (CreateSkeltonFromData(Time));
			_updateCount = 0;
			_syncPositionList.Callback = (op, i) => _updateCount++;
			_syncRotationList.Callback = (op, i) => _updateCount++;
			_syncScaleList.Callback = (op, i) => _updateCount++;
		}
		void ApplyData () {
			if (_updateCount > 0) {
				_updateCount = 0;
				_datastream.Add (CreateSkeltonFromData(Time));
			}

			if (_datastream.Count <= 0)
				return;

			var tnow = Time;
			var tinterp = -latency * GetNetworkSendInterval () + tnow;
			while (_datastream.Count >= 2 && _datastream [1].time < tinterp)
				_datastream.RemoveAt (0);

			var d0 = _datastream [0];
			var d1 = d0;
			if (_datastream.Count >= 2)
				d1 = _datastream [1];

			var dt = d1.time - d0.time;
			if (dt > Mathf.Epsilon) {
				var t = (tinterp - d0.time) / dt;
				_tmpdata0.Interpolate (d0, d1, t);
				d0 = _tmpdata0;
			}
            d0.Load (_bones);
		}
		#endregion

		#region Classes
		public struct Bone {
			[System.Flags]
			public enum ChangeFlags { 
				None = 0, Position = 1 << 0, Rotation = 1 << 1, Scale = 1 << 2,
				All = Position | Rotation | Scale
			}
			public Vector3 position;
			public Quaternion rotation;
			public Vector3 scale;

			public Bone(Transform tr) {
				position = tr.localPosition;
				rotation = tr.localRotation;
				scale = tr.localScale;
			}
			public void Load(Transform tr) {
				tr.localPosition = position;
				tr.localRotation = rotation;
				tr.localScale = scale;
			}
			public void Interpolate(Bone d0, Bone d1, float t) {
				position = Vector3.Lerp (d0.position, d1.position, t);
				rotation = Quaternion.Lerp (d0.rotation, d1.rotation, t);
				scale = Vector3.Lerp (d0.scale, d1.scale, t);
			}
			public ChangeFlags Changed(Bone prev) {
				return (position != prev.position ? ChangeFlags.Position : 0)
					| (rotation != prev.rotation ? ChangeFlags.Rotation : 0)
					| (scale != prev.scale ? ChangeFlags.Scale : 0);
			}
		}
		public struct Skelton {
			public float time;
			public Bone[] bones;

			public Skelton Save(float srcTime, Transform[] src) {
                time = srcTime;
				for (var i = 0; i < src.Length; i++)
					this.bones [i] = new Bone (src[i]);
				return this;
			}
            public void Load(Transform[] dst) {
                for (var i = 0; i < bones.Length; i++)
                    bones [i].Load (dst [i]);
            }
			public void Interpolate(Skelton d0, Skelton d1, float t) {
				time = Mathf.Lerp (d0.time, d1.time, t);
                for (var i = 0; i < bones.Length; i++)
					bones [i].Interpolate (d0.bones [i], d1.bones [i], t);
			}
			public void Clone(Skelton src) {
				time = src.time;
                for (var i = 0; i < bones.Length; i++)
					bones [i] = src.bones [i];
			}
		}
		#endregion
	}
}
