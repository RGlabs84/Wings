using UnityEngine;
using WingsoftheValkyrie;

namespace WingsoftheValkyrie.VFX
{
    public class RuneWingVFX : MonoBehaviour
    {
        private Player _player;
        private ParticleSystem _leftWing;
        private ParticleSystem _rightWing;
        
        private Transform _leftRoot;
        private Transform _rightRoot;

        private LineRenderer[] _leftLines = new LineRenderer[7];
        private LineRenderer[] _rightLines = new LineRenderer[7];
        
        private MeshFilter _leftMeshFilter;
        private MeshFilter _rightMeshFilter;
        private Mesh _leftMesh;
        private Mesh _rightMesh;
        
        private bool _isGliding = false;
        private float _flapTimer = 0f;
        private Color _currentTierColor = Color.clear;
        
        private Vector3 _lastEmitPosL;
        private Vector3 _lastEmitPosR;
        
        public bool IsGlidingLocal = false;
        public bool WantsToFlap = false;

        /// <summary>Monotonic flap tick published on the owner's ZDO.</summary>
        public int FlapCount = 0;

        /// <summary>Last flap tick replayed for a remote player; int.MinValue = not seen yet.</summary>
        public int LastSeenFlapCount = int.MinValue;

        /// <summary>Cached so the flight controller does not GetComponent every frame per player.</summary>
        public ZNetView NView { get; private set; }

        private Color[] _whiteColors;

        private void Awake()
        {
            _player = GetComponent<Player>();
            NView = GetComponent<ZNetView>();
        }

        public void EnsureEmitters()
        {
            if (_leftWing != null && _rightWing != null)
                return;

            Transform spine = _player.transform;

            Texture2D softTex = ValkyrieRuneTextureGenerator.GenerateSoftTrail(Color.white);
            Material softMat = ValkyrieRuneVFX_Helper.GetOrCreateRuneMaterial(softTex, Color.white);
            
            Texture2D atlasTex = ValkyrieRuneTextureGenerator.GenerateRuneAtlas(Color.white);
            Material atlasMat = ValkyrieRuneVFX_Helper.GetOrCreateRuneMaterial(atlasTex, Color.white);

            _leftRoot = new GameObject("RuneWing_Left").transform;
            _leftRoot.gameObject.layer = _player.gameObject.layer;
            _leftRoot.SetParent(spine, false);
            _leftRoot.localPosition = new Vector3(-0.3f, 1.2f, -0.3f);
            
            _rightRoot = new GameObject("RuneWing_Right").transform;
            _rightRoot.gameObject.layer = _player.gameObject.layer;
            _rightRoot.SetParent(spine, false);
            _rightRoot.localPosition = new Vector3(0.3f, 1.2f, -0.3f);
            
            InitWingObjects(_leftRoot.gameObject, softMat, atlasMat, false, ref _leftLines, out _leftMeshFilter, out _leftMesh);
            InitWingObjects(_rightRoot.gameObject, softMat, atlasMat, true, ref _rightLines, out _rightMeshFilter, out _rightMesh);

            _leftWing = CreateParticleSystem(_leftRoot.gameObject, atlasMat);
            _rightWing = CreateParticleSystem(_rightRoot.gameObject, atlasMat);
            
            UpdateWingMeshes(0f, 0f);
        }

        private void InitWingObjects(GameObject root, Material lineMat, Material atlasMat, bool isRight, ref LineRenderer[] lines, out MeshFilter meshFilter, out Mesh mesh)
        {
            // Lines
            for (int i = 0; i < 7; i++)
            {
                GameObject bone = new GameObject("BoneLine_" + i);
                bone.layer = root.layer;
                bone.transform.SetParent(root.transform, false);
                var lr = bone.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.material = lineMat;
                lr.enabled = false;
                lines[i] = lr;
            }

            // Mesh
            mesh = new Mesh();
            mesh.MarkDynamic();
            
            // CRITICAL: Unity drops triangle and UV assignments if the mesh doesn't have vertices yet!
            mesh.vertices = new Vector3[23];
            
            // Particle shaders often multiply texture by vertex color. 
            // Since we use a particle shader for the membrane, we MUST provide white vertex colors.
            if (_whiteColors == null)
            {
                _whiteColors = new Color[23];
                for (int i = 0; i < 23; i++) _whiteColors[i] = Color.white;
            }
            mesh.colors = _whiteColors;
            
            // UVs and Triangles remain constant
            Vector2[] uvs = new Vector2[23];
            uvs[0] = new Vector2(0, 1) * 3f;
            uvs[1] = new Vector2(0.2f, 0.8f) * 3f;
            uvs[2] = new Vector2(0.3f, 1) * 3f;
            uvs[3] = new Vector2(0.5f, 0.9f) * 3f; uvs[4] = new Vector2(0.7f, 0.8f) * 3f; uvs[5] = new Vector2(1, 0.8f) * 3f;
            uvs[6] = new Vector2(0.5f, 0.6f) * 3f; uvs[7] = new Vector2(0.7f, 0.5f) * 3f; uvs[8] = new Vector2(1, 0.5f) * 3f;
            uvs[9] = new Vector2(0.4f, 0.4f) * 3f; uvs[10] = new Vector2(0.6f, 0.3f) * 3f; uvs[11] = new Vector2(0.9f, 0.2f) * 3f;
            uvs[12] = new Vector2(0.3f, 0.2f) * 3f; uvs[13] = new Vector2(0.4f, 0.1f) * 3f; uvs[14] = new Vector2(0.6f, 0) * 3f;
            uvs[15] = new Vector2(0, 0) * 3f;
            mesh.uv = uvs;

            int[] tris = new int[] {
                0, 1, 2,   
                2, 6, 3,   3, 6, 7,   3, 7, 4,   4, 7, 8,   4, 8, 5,
                2, 9, 6,   6, 9, 10,  6, 10, 7,  7, 10, 11, 7, 11, 8,
                2, 12, 9,  9, 12, 13, 9, 13, 10, 10, 13, 14, 10, 14, 11,
                2, 1, 12,  1, 0, 12,  0, 15, 12, 15, 13, 12, 15, 14, 13
            };
            int[] doubleTris = new int[tris.Length * 2];
            for (int i = 0; i < tris.Length; i+=3) {
                doubleTris[i] = tris[i]; doubleTris[i+1] = tris[i+1]; doubleTris[i+2] = tris[i+2];
                doubleTris[tris.Length + i] = tris[i]; doubleTris[tris.Length + i+1] = tris[i+2]; doubleTris[tris.Length + i+2] = tris[i+1];
            }
            mesh.triangles = doubleTris;

            GameObject membrane = new GameObject("Membrane");
            membrane.layer = root.layer;
            membrane.transform.SetParent(root.transform, false);
            meshFilter = membrane.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            var mr = membrane.AddComponent<MeshRenderer>();
            mr.material = atlasMat;
            mr.enabled = false;
        }

        private Vector3[] GetBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, int segments)
        {
            Vector3[] points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float u = 1 - t;
                points[i] = u * u * p0 + 2 * u * t * p1 + t * t * p2;
            }
            return points;
        }

        private void SetupBoneLine(LineRenderer lr, Vector3[] points, float startWidth, float endWidth)
        {
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.startWidth = startWidth;
            lr.endWidth = endWidth;
            
            if (_currentTierColor != Color.clear)
            {
                lr.startColor = _currentTierColor;
                lr.endColor = new Color(_currentTierColor.r, _currentTierColor.g, _currentTierColor.b, 0.2f);
            }
        }

        private ParticleSystem CreateParticleSystem(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startSpeed = 0f; // Stay in place for a perfect trail
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f); // Made runes smaller again based on feedback
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            
            var emission = ps.emission;
            emission.enabled = false; // We will use foolproof manual emission in Update 

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 3.0f;  

            var texModule = ps.textureSheetAnimation;
            texModule.enabled = true;
            texModule.numTilesX = 4;
            texModule.numTilesY = 4;
            texModule.animation = ParticleSystemAnimationType.WholeSheet;
            texModule.startFrame = new ParticleSystem.MinMaxCurve(0, 15);
            texModule.timeMode = ParticleSystemAnimationTimeMode.Lifetime;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (mat != null) renderer.material = mat;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
        
        public void SetTierColor(Color c)
        {
            if (_currentTierColor == c) return;
            _currentTierColor = c;

            if (_leftWing == null || _rightWing == null) EnsureEmitters();
            
            var mainL = _leftWing.main;
            mainL.startColor = c;
            var mainR = _rightWing.main;
            mainR.startColor = c;
            
            var prL = _leftWing.GetComponent<ParticleSystemRenderer>();
            if (prL != null && prL.material != null)
            {
                if (prL.material.HasProperty("_Color")) prL.material.SetColor("_Color", c);
                if (prL.material.HasProperty("_TintColor")) prL.material.SetColor("_TintColor", c);
                if (prL.material.HasProperty("_EmissionColor")) prL.material.SetColor("_EmissionColor", c);
            }
            
            var prR = _rightWing.GetComponent<ParticleSystemRenderer>();
            if (prR != null && prR.material != null)
            {
                if (prR.material.HasProperty("_Color")) prR.material.SetColor("_Color", c);
                if (prR.material.HasProperty("_TintColor")) prR.material.SetColor("_TintColor", c);
                if (prR.material.HasProperty("_EmissionColor")) prR.material.SetColor("_EmissionColor", c);
            }

            foreach (var lr in _leftLines) 
            { 
                lr.startColor = c; lr.endColor = new Color(c.r, c.g, c.b, 0.2f); 
                if (lr != null && lr.material != null)
                {
                    if (lr.material.HasProperty("_Color")) lr.material.SetColor("_Color", c);
                    if (lr.material.HasProperty("_TintColor")) lr.material.SetColor("_TintColor", c);
                    if (lr.material.HasProperty("_EmissionColor")) lr.material.SetColor("_EmissionColor", c);
                }
            }
            
            foreach (var lr in _rightLines) 
            { 
                lr.startColor = c; lr.endColor = new Color(c.r, c.g, c.b, 0.2f); 
                if (lr != null && lr.material != null)
                {
                    if (lr.material.HasProperty("_Color")) lr.material.SetColor("_Color", c);
                    if (lr.material.HasProperty("_TintColor")) lr.material.SetColor("_TintColor", c);
                    if (lr.material.HasProperty("_EmissionColor")) lr.material.SetColor("_EmissionColor", c);
                }
            }

            var leftMr = _leftMeshFilter.GetComponent<MeshRenderer>();
            if (leftMr.material.HasProperty("_Color")) leftMr.material.SetColor("_Color", new Color(c.r, c.g, c.b, 0.15f)); 
            if (leftMr.material.HasProperty("_TintColor")) leftMr.material.SetColor("_TintColor", new Color(c.r, c.g, c.b, 0.15f)); 
            if (leftMr.material.HasProperty("_EmissionColor")) leftMr.material.SetColor("_EmissionColor", new Color(c.r, c.g, c.b, 0.05f)); 
            
            var rightMr = _rightMeshFilter.GetComponent<MeshRenderer>();
            if (rightMr.material.HasProperty("_Color")) rightMr.material.SetColor("_Color", new Color(c.r, c.g, c.b, 0.15f)); 
            if (rightMr.material.HasProperty("_TintColor")) rightMr.material.SetColor("_TintColor", new Color(c.r, c.g, c.b, 0.15f)); 
            if (rightMr.material.HasProperty("_EmissionColor")) rightMr.material.SetColor("_EmissionColor", new Color(c.r, c.g, c.b, 0.05f)); 
        }
        
        public void SetGlidingState(bool gliding)
        {
            if (_isGliding == gliding) return;
            _isGliding = gliding;
            
            if (_leftWing == null || _rightWing == null) EnsureEmitters();

            if (_isGliding)
            {
                if (!_leftWing.isPlaying) _leftWing.Play();
                if (!_rightWing.isPlaying) _rightWing.Play();
                foreach (var lr in _leftLines) lr.enabled = true;
                foreach (var lr in _rightLines) lr.enabled = true;
                _leftMeshFilter.GetComponent<MeshRenderer>().enabled = true;
                _rightMeshFilter.GetComponent<MeshRenderer>().enabled = true;
            }
            else
            {
                _leftWing.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _rightWing.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var lr in _leftLines) lr.enabled = false;
                foreach (var lr in _rightLines) lr.enabled = false;
                _leftMeshFilter.GetComponent<MeshRenderer>().enabled = false;
                _rightMeshFilter.GetComponent<MeshRenderer>().enabled = false;
            }
        }

        public void TriggerFlap()
        {
            if (_leftWing == null || _rightWing == null) EnsureEmitters();
            _flapTimer = 0.4f;
            
            if (!_isGliding)
            {
                _leftWing.Emit(15);
                _rightWing.Emit(15);
            }
        }

        private void Update()
        {
            if (_leftRoot == null || _rightRoot == null) return;

            float scale = ModConfig.GlobalWingSpan?.Value ?? 1.0f;
            _leftRoot.localScale = new Vector3(scale, scale, scale);
            _rightRoot.localScale = new Vector3(scale, scale, scale);

            float flapProgress = 0f;
            if (_flapTimer > 0)
            {
                _flapTimer -= Time.deltaTime;
                flapProgress = 1f - Mathf.Clamp01(_flapTimer / 0.4f);
            }

            // Foolproof manual runic trail emission
            if (_isGliding && _leftWing != null && _rightWing != null)
            {
                if (Vector3.Distance(_leftRoot.position, _lastEmitPosL) > 0.1f)
                {
                    _leftWing.Emit(1);
                    _lastEmitPosL = _leftRoot.position;
                }
                if (Vector3.Distance(_rightRoot.position, _lastEmitPosR) > 0.1f)
                {
                    _rightWing.Emit(1);
                    _lastEmitPosR = _rightRoot.position;
                }
            }

            float idleTime = _isGliding && _flapTimer <= 0 ? Time.time : 0f;
            UpdateWingMeshes(flapProgress, idleTime);

            // Flapping rotation on the root
            float flapAngle = 0f;
            if (flapProgress > 0)
            {
                if (flapProgress < 0.2f) flapAngle = Mathf.Lerp(0, 20f, flapProgress / 0.2f);
                else if (flapProgress < 0.5f) flapAngle = Mathf.Lerp(20f, -50f, (flapProgress - 0.2f) / 0.3f);
                else flapAngle = Mathf.Lerp(-50f, 0f, (flapProgress - 0.5f) / 0.5f);
            }
            else if (idleTime > 0)
            {
                flapAngle = Mathf.Sin(idleTime * 4f) * 5f;
            }

            float sweepAngle = (idleTime > 0) ? Mathf.Cos(idleTime * 2f) * 2f : 0f;

            _leftRoot.localRotation = Quaternion.Lerp(_leftRoot.localRotation, Quaternion.Euler(sweepAngle, -20 + sweepAngle, flapAngle), Time.deltaTime * 15f);
            _rightRoot.localRotation = Quaternion.Lerp(_rightRoot.localRotation, Quaternion.Euler(-sweepAngle, 20 - sweepAngle, -flapAngle), Time.deltaTime * 15f);
        }

        private void UpdateWingMeshes(float flapProgress, float idleTime)
        {
            UpdateSingleWing(false, _leftLines, _leftMesh, flapProgress, idleTime);
            UpdateSingleWing(true, _rightLines, _rightMesh, flapProgress, idleTime);
        }

        private void UpdateSingleWing(bool isRight, LineRenderer[] lines, Mesh mesh, float flapProgress, float idleTime)
        {
            float f = isRight ? 1f : -1f;

            float foldZ = 0f; 
            float foldY = 0f; 

            if (flapProgress > 0)
            {
                if (flapProgress < 0.2f) {
                    float t = flapProgress / 0.2f;
                    foldZ = Mathf.Lerp(0, -0.8f, t);
                    foldY = Mathf.Lerp(0, -0.5f, t);
                } 
                else if (flapProgress < 0.5f) {
                    float t = (flapProgress - 0.2f) / 0.3f;
                    foldZ = Mathf.Lerp(-0.8f, 0.2f, t); 
                    foldY = Mathf.Lerp(-0.5f, 0.3f, t);
                } 
                else {
                    float t = (flapProgress - 0.5f) / 0.5f;
                    foldZ = Mathf.Lerp(0.2f, 0f, t);
                    foldY = Mathf.Lerp(0.3f, 0f, t);
                }
            }
            else if (idleTime > 0)
            {
                foldZ = Mathf.Sin(idleTime * 2f) * 0.1f;
                foldY = Mathf.Cos(idleTime * 4f) * 0.1f;
            }

            Vector3 p_shoulder = new Vector3(0, 0, 0);
            Vector3 p_elbow    = new Vector3(0.8f * f, 0.2f, 0.4f);
            
            Vector3 p_wrist    = new Vector3((2.2f + foldZ * 0.5f) * f, 0.5f + foldY * 0.5f, 0.2f + foldZ);
            
            Vector3 p_thumb1   = p_wrist + new Vector3(0.2f * f, 0.2f, 0.1f);
            Vector3 p_thumb2   = p_wrist + new Vector3(0.3f * f, 0.4f, 0.2f);

            Vector3 p_f1_ctrl = p_wrist + new Vector3(1.6f * f, -0.1f + foldY, -0.4f + foldZ);
            Vector3 p_f1_tip  = p_wrist + new Vector3((2.8f + foldZ) * f, -0.3f + foldY * 2f, -1.2f + foldZ * 2f); 

            Vector3 p_f2_ctrl = p_wrist + new Vector3(1.3f * f, -0.4f + foldY, -0.8f + foldZ);
            Vector3 p_f2_tip  = p_wrist + new Vector3((2.4f + foldZ) * f, -0.7f + foldY * 2f, -1.8f + foldZ * 2f); 

            Vector3 p_f3_ctrl = p_wrist + new Vector3(0.8f * f, -0.7f + foldY, -1.2f + foldZ);
            Vector3 p_f3_tip  = p_wrist + new Vector3((1.6f + foldZ) * f, -1.0f + foldY * 2f, -2.4f + foldZ * 2f); 

            Vector3 p_f4_ctrl = p_wrist + new Vector3(0.1f * f, -0.9f + foldY, -1.4f + foldZ);
            Vector3 p_f4_tip  = p_wrist + new Vector3((0.4f + foldZ) * f, -1.2f + foldY * 2f, -2.8f + foldZ * 2f); 
            
            Vector3 p_body = new Vector3(0.2f * f, -0.5f, -0.5f);

            Vector3[] bone_arm = GetBezierCurve(p_shoulder, p_elbow, p_wrist, 10);
            Vector3[] bone_f1 = GetBezierCurve(p_wrist, p_f1_ctrl, p_f1_tip, 10);
            Vector3[] bone_f2 = GetBezierCurve(p_wrist, p_f2_ctrl, p_f2_tip, 10);
            Vector3[] bone_f3 = GetBezierCurve(p_wrist, p_f3_ctrl, p_f3_tip, 10);
            Vector3[] bone_f4 = GetBezierCurve(p_wrist, p_f4_ctrl, p_f4_tip, 10);

            SetupBoneLine(lines[0], bone_arm, 0.25f, 0.15f);
            SetupBoneLine(lines[1], new Vector3[] { p_wrist, p_thumb1, p_thumb2 }, 0.08f, 0.01f);
            SetupBoneLine(lines[2], bone_f1, 0.12f, 0.01f);
            SetupBoneLine(lines[3], bone_f2, 0.12f, 0.01f);
            SetupBoneLine(lines[4], bone_f3, 0.12f, 0.01f);
            SetupBoneLine(lines[5], bone_f4, 0.12f, 0.01f);
            SetupBoneLine(lines[6], new Vector3[] { p_shoulder, p_body }, 0.1f, 0.05f);

            Vector3[] verts = new Vector3[23];
            verts[0] = p_shoulder;
            verts[1] = bone_arm[5];
            verts[2] = p_wrist;
            verts[3] = bone_f1[2]; verts[4] = bone_f1[6]; verts[5] = p_f1_tip;
            verts[6] = bone_f2[2]; verts[7] = bone_f2[6]; verts[8] = p_f2_tip;
            verts[9] = bone_f3[2]; verts[10] = bone_f3[6]; verts[11] = p_f3_tip;
            verts[12] = bone_f4[2]; verts[13] = bone_f4[6]; verts[14] = p_f4_tip;
            verts[15] = p_body;

            mesh.vertices = verts;
            if (_whiteColors != null) mesh.colors = _whiteColors; // Prevent Unity from occasionally dropping vertex colors
            mesh.RecalculateNormals();
            mesh.RecalculateBounds(); // CRITICAL: Prevents Unity from culling the dynamic membrane mesh
        }
    }
}
