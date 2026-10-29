using System.Collections.Generic;
using Klak.TestTools;
using MediaPipe.HandPose;
using UnityEngine;
using UnityEngine.VFX;

namespace HandTrackingVFX
{
    public sealed class HandToVFX : MonoBehaviour
    {
        public const int JointCount = HandPipeline.KeyPointCount; // 21
        public const int TotalJoints = JointCount * 2; // 42

        [Header("Input")]
        [SerializeField] ImageSource _source;
        [SerializeField] ResourceSet _resources;
        [SerializeField] bool _useAsyncReadback = true;

        [Header("Hand Space")]
        [SerializeField] Transform _handRoot;
        [SerializeField] bool _flipX = true;
        [SerializeField, Range(0.1f, 10f)] float _depthScale = 1.0f;

        [Header("Joints")]
        [SerializeField] GameObject _jointPrefab;
        [SerializeField] bool _hideUntilDetected = true;

        [Header("Gizmos")]
        [SerializeField] bool _drawJointGizmos = true;
        [SerializeField, Range(0.0001f, 0.1f)] float _gizmoSize = 0.01f;
        [SerializeField] bool _drawJointLabels;
        [SerializeField] bool _drawBones = true;

        [Header("VFX target (optional)")]
        [SerializeField] VisualEffect _targetVFX;
        [SerializeField] string _positionMapProperty = "PositionMap";
        [SerializeField] string _jointCountProperty = "JointCount";

        public Texture PositionMap => _positionMap;
        public Transform[] Joints => _joints;
        public bool IsTracking => _hasReadback0 || _hasReadback1;

        HandPipeline _pipeline0;
        HandPipeline _pipeline1;
        Texture2D _positionMap;
        Transform[] _joints;
        readonly Vector3[] _cache0 = new Vector3[JointCount];
        readonly Vector3[] _cache1 = new Vector3[JointCount];
        readonly Vector3[] _prevWorld0 = new Vector3[JointCount];
        readonly Vector3[] _prevWorld1 = new Vector3[JointCount];
        readonly float[] _speed0 = new float[JointCount];
        readonly float[] _speed1 = new float[JointCount];
        readonly Color[] _pixelBuffer = new Color[64];
        bool _hasReadback0;
        bool _hasReadback1;

        void Awake()
        {
            if (_handRoot == null) _handRoot = transform;

            _positionMap = new Texture2D(64, 1, TextureFormat.RGBAFloat, false, true)
            {
                name = "HandPositionMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };

            _joints = new Transform[TotalJoints];
            for (var i = 0; i < TotalJoints; i++)
            {
                GameObject go;
                if (_jointPrefab != null)
                    go = Instantiate(_jointPrefab, _handRoot);
                else
                {
                    go = new GameObject();
                    go.transform.SetParent(_handRoot, false);
                }
                go.name = $"Joint_{i:00}";
                if (_hideUntilDetected) go.SetActive(false);
                _joints[i] = go.transform;
            }
        }

        void Start()
        {
            _pipeline0 = new HandPipeline(_resources) { UseAsyncReadback = _useAsyncReadback };
            _pipeline1 = new HandPipeline(_resources) { UseAsyncReadback = _useAsyncReadback };
        }

        void OnDestroy()
        {
            _pipeline0?.Dispose();
            _pipeline1?.Dispose();
            if (_positionMap != null) Destroy(_positionMap);
        }

        void LateUpdate()
        {
            if (_pipeline0 == null || _source == null || _source.Texture == null) return;

            var tex = _source.Texture;
            _pipeline0.UseAsyncReadback = _useAsyncReadback;
            _pipeline1.UseAsyncReadback = _useAsyncReadback;
            _pipeline0.ProcessImage(tex);
            _pipeline1.ProcessImage(tex);

            UpdateCache(_pipeline0, _cache0, ref _hasReadback0, 0);
            UpdateCache(_pipeline1, _cache1, ref _hasReadback1, JointCount);

            UpdatePositionMap();

            if (_targetVFX != null)
            {
                if (!string.IsNullOrEmpty(_positionMapProperty) && _targetVFX.HasTexture(_positionMapProperty))
                    _targetVFX.SetTexture(_positionMapProperty, _positionMap);
                if (!string.IsNullOrEmpty(_jointCountProperty) && _targetVFX.HasUInt(_jointCountProperty))
                    _targetVFX.SetUInt(_jointCountProperty, (uint)TotalJoints);
            }
        }

        void UpdateCache(HandPipeline pipeline, Vector3[] cache, ref bool hasReadback, int jointOffset)
        {
            for (var i = 0; i < JointCount; i++)
            {
                var p = pipeline.GetKeyPoint(i);
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) ||
                    p.sqrMagnitude < 0.0001f ||
                    (cache[i] != Vector3.zero && Vector3.Distance(p, cache[i]) > 5.0f))
                    p = cache[i];
                if (_flipX) p.x = -p.x;
                p.z *= _depthScale;
                cache[i] = p;
                _joints[jointOffset + i].localPosition = p;
            }

            if (!hasReadback && cache[0].sqrMagnitude > 0.0001f)
            {
                hasReadback = true;
                if (_hideUntilDetected)
                    for (var i = 0; i < JointCount; i++)
                        _joints[jointOffset + i].gameObject.SetActive(true);
            }
        }

        void UpdatePositionMap()
        {
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var alpha = IsTracking ? Mathf.Clamp01(dt / 0.1f) : 1f;

            WriteHandToBuffer(_cache0, _prevWorld0, _speed0, alpha, dt, 0);
            WriteHandToBuffer(_cache1, _prevWorld1, _speed1, alpha, dt, JointCount);

            _positionMap.SetPixels(_pixelBuffer);
            _positionMap.Apply(false, false);
        }

        void WriteHandToBuffer(Vector3[] cache, Vector3[] prevWorld, float[] speed, float alpha, float dt, int offset)
        {
            for (var i = 0; i < 32; i++)
            {
                if (i < JointCount)
                {
                    var w = _handRoot.TransformPoint(cache[i]);
                    var instSpeed = (w - prevWorld[i]).magnitude / dt;
                    speed[i] = Mathf.Lerp(speed[i], instSpeed, alpha);
                    prevWorld[i] = w;
                    _pixelBuffer[offset + i] = new Color(w.x, w.y, w.z, speed[i]);
                }
                else
                {
                    _pixelBuffer[offset + i] = new Color(0, 0, 0, 0);
                }
            }
        }

        static readonly int[][] _fingers =
        {
            new[] { 0, 1, 2, 3, 4 },
            new[] { 0, 5, 6, 7, 8 },
            new[] { 0, 9, 10, 11, 12 },
            new[] { 0, 13, 14, 15, 16 },
            new[] { 0, 17, 18, 19, 20 },
        };

        static readonly Color[] _fingerColors =
        {
            new Color(1f, 0.45f, 0.2f),
            new Color(1f, 0.9f, 0.2f),
            new Color(0.4f, 1f, 0.4f),
            new Color(0.3f, 0.7f, 1f),
            new Color(0.9f, 0.4f, 1f),
        };

        void OnDrawGizmos()
        {
            if (!_drawJointGizmos) return;
            if (_joints == null || _joints.Length != TotalJoints) return;

            for (var h = 0; h < 2; h++)
            {
                var offset = h * JointCount;
                for (var f = 0; f < _fingers.Length; f++)
                {
                    var chain = _fingers[f];
                    Gizmos.color = _fingerColors[f];
                    for (var k = 0; k < chain.Length; k++)
                    {
                        var idx = chain[k];
                        var t = _joints[offset + idx];
                        if (t == null) continue;
                        Gizmos.DrawSphere(t.position, _gizmoSize);
                        if (_drawBones && k > 0)
                        {
                            var prev = _joints[offset + chain[k - 1]];
                            if (prev != null) Gizmos.DrawLine(prev.position, t.position);
                        }
#if UNITY_EDITOR
                        if (_drawJointLabels)
                            UnityEditor.Handles.Label(t.position + Vector3.up * _gizmoSize * 1.5f, idx.ToString());
#endif
                    }
                }
            }
        }
    }
}