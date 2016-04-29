﻿using UnityEngine;
using System.Collections;
using Gist;
using DataUI;

namespace Gist {
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class CropCamera : MonoBehaviour {
        public Data data;

        public bool debugUI;
        public KeyCode debugKey = KeyCode.C;

        Camera _attachedCamera;
        Rect _window = new Rect(10, 10, 200, 100);
        FieldEditor _editor;

    	void OnEnable() {
            _attachedCamera = GetComponent<Camera> ();
            _editor = new FieldEditor (data);
    	}
        void OnDisable() {
            if (_attachedCamera != null)
                _attachedCamera.ResetProjectionMatrix ();
        }
    	void Update () {
            if (Input.GetKeyDown (debugKey)) {
                debugUI = !debugUI;
            }

            var totalSize = data.totalSize;
            var normCropX = 1f / totalSize.x;
            var normCropY = 1f / totalSize.y;
            var normOffsetX = 2f * (data.offset.x + 0.5f) / totalSize.x - 1f;
            var normOffsetY = 2f * (data.offset.y + 0.5f) / totalSize.y - 1f;
            var totalAspect = _attachedCamera.aspect * data.totalSize.x / totalSize.y;

            float left, right, bottom, top;
            LensShift.NearPlane (_attachedCamera.nearClipPlane, totalAspect, _attachedCamera.fieldOfView, 
                out left, out right, out bottom, out top);

            var cropRight = right * (normCropX + normOffsetX);
            var cropLeft = right * (-normCropX + normOffsetX);
            var cropTop = top * (normCropY + normOffsetY);
            var cropBottom = top * (-normCropY + normOffsetY);
            _attachedCamera.Perspective (cropLeft, cropRight, cropBottom, cropTop, _attachedCamera.nearClipPlane, _attachedCamera.farClipPlane);
    	}
        void OnGUI() {
            if (debugUI && _attachedCamera != null)
                _window = GUILayout.Window (GetInstanceID (), _window, Window, "CropCamera");
        }

        void Window(int id) {
            GUILayout.BeginVertical ();
            _editor.OnGUI ();
            GUILayout.EndVertical ();
            GUI.DragWindow ();
        }

        [System.Serializable]
        public struct IntVector2 {
            public int x;
            public int y;

            public IntVector2(int x, int y) {
                this.x = x;
                this.y = y;
            }

            public static Vector2 operator/ (IntVector2 t, IntVector2 b) {
                return new Vector2((float)t.x / b.x, (float)t.y / b.y);
            }
        }
        [System.Serializable]
        public class Data {
            public Vector2 totalSize = new Vector2(2f, 2f);
            public Vector2 offset = new Vector2(1f, 1f);
        }
    }
}