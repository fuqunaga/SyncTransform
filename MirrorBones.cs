using UnityEngine;
using System.Collections;

namespace SyncTransformSystem {
    [RequireComponent(typeof(Animator))]
    public class MirrorBones : MonoBehaviour {
        
        public Animator src;

        Transform[] _src;
        Transform[] _dst;

        public static void SyncBones(Transform[] src, Transform[] dst) {
            for (var i = 0; i < src.Length; i++) {
                var str = src [i];
                var dtr = dst [i];
                dtr.localPosition = str.localPosition;
                dtr.localRotation = str.localRotation;
                dtr.localScale = str.localScale;
            }
        }

        void Awake() {
            _src = SyncAnimation.BoneList (src.transform, false);
            _dst = SyncAnimation.BoneList (transform, false);

			GetComponent<Animator> ().enabled = false;

            Debug.LogFormat ("Bone count={0}", _src.Length);
        }
    	void Update () {
            if (_src == null || _dst == null || _src.Length != _dst.Length) {
                Debug.LogError ("Incompatible bones");
                return;
            }

            SyncBones (_src, _dst);
    	}
    }
}
