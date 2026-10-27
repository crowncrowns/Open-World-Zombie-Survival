using System;
using UnityEngine;

namespace ZombieSurvival.Furniture
{
    /// <summary>A real coiled tube between authored anchors. Its pose derives entirely from its furniture mechanism.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class FurnitureCordVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("Неподвижная деталь корпуса, к которой подключён провод.")]
        private Transform fixedPart;
        [SerializeField, Tooltip("Точка выхода провода в локальных координатах корпуса, м.")]
        private Vector3 fixedAnchor;
        [SerializeField, Tooltip("Подвижная трубка или другая деталь, к которой подключён второй конец.")]
        private Transform movingPart;
        [SerializeField, Tooltip("Точка подключения в локальных координатах подвижной детали, м.")]
        private Vector3 movingAnchor;
        [SerializeField, Min(.0001f), Tooltip("Радиус самой резиновой жилы, м.")]
        private float tubeRadius = .0012f;
        [SerializeField, Min(0f), Tooltip("Радиус витков телефонного шнура, м.")]
        private float coilRadius = .0055f;
        [SerializeField, Range(1, 40), Tooltip("Число витков LOD0. Дальние LOD используют упрощённый рисунок витков.")]
        private int turns = 20;
        [SerializeField, Min(.005f), Tooltip("Вынос свободной дуги влево от корпуса, м. Концы остаются на своих разъёмах.")]
        private float sideSlack = .07f;
        [SerializeField, Tooltip("Общий материал резины. Компонент не создаёт копии материалов.")]
        private Material sharedMaterial;
        [SerializeField, HideInInspector] private MeshFilter[] filters = new MeshFilter[3];
        [SerializeField, HideInInspector] private MeshRenderer[] renderers = new MeshRenderer[3];
        [SerializeField, HideInInspector] private Mesh[] previewMeshes = new Mesh[3];
        private readonly Mesh[] dynamicMeshes = new Mesh[3];
        private readonly Buffers[] buffers = new Buffers[3];
        private Vector3 previousFixed, previousMoving, previousOutward;
        private bool initialized;
        public int Revision { get; private set; }
        public Renderer[] CordRenderers => renderers;
        public Vector3 FixedWorldAnchor => fixedPart != null ? fixedPart.TransformPoint(fixedAnchor) : transform.position;
        public Vector3 MovingWorldAnchor => movingPart != null ? movingPart.TransformPoint(movingAnchor) : transform.position;
        public Mesh[] CurrentMeshes => new[] { filters[0] != null ? filters[0].sharedMesh : null,
            filters[1] != null ? filters[1].sharedMesh : null, filters[2] != null ? filters[2].sharedMesh : null };
        sealed class Buffers
        {
            public int samples, sides;
            public Vector3[] centres, vertices, normals;
            public Vector4[] tangents;
            public Vector2[] uv, uv2;
            public int[] triangles;
        }

        public void Configure(Transform body, Vector3 bodyLocalAnchor, Transform handset, Vector3 handsetLocalAnchor,
            Material rubber, float wireRadius = .0012f, float springRadius = .0055f, int coilTurns = 20, float slack = .07f)
        {
            if (body == null || handset == null || rubber == null) throw new ArgumentException("Проводу нужны обе опоры и общий материал.");
            fixedPart = body; fixedAnchor = bodyLocalAnchor; movingPart = handset; movingAnchor = handsetLocalAnchor;
            sharedMaterial = rubber; tubeRadius = Mathf.Max(.0001f, wireRadius); coilRadius = Mathf.Max(0f, springRadius);
            turns = Mathf.Clamp(coilTurns, 1, 40); sideSlack = Mathf.Max(.005f, slack);
            EnsureChildren(); initialized = false; RebuildIfNeeded();
        }
        private void OnEnable()
        {
            if (!gameObject.scene.IsValid() || fixedPart == null || movingPart == null) return;
            EnsureChildren(); initialized = false; RebuildIfNeeded();
        }
        private void LateUpdate() { if (gameObject.scene.IsValid()) RebuildIfNeeded(); }
        private void OnValidate()
        {
            tubeRadius=Mathf.Max(.0001f,tubeRadius);coilRadius=Mathf.Max(0f,coilRadius);
            turns=Mathf.Clamp(turns,1,40);sideSlack=Mathf.Max(.005f,sideSlack);initialized=false;
        }
        private void OnDisable() => ReleaseDynamicMeshes();
        private void OnDestroy() => ReleaseDynamicMeshes();
        private void EnsureChildren()
        {
            if (filters == null || filters.Length != 3) filters = new MeshFilter[3];
            if (renderers == null || renderers.Length != 3) renderers = new MeshRenderer[3];
            if (previewMeshes == null || previewMeshes.Length != 3) previewMeshes = new Mesh[3];
            for (int lod = 0; lod < 3; lod++)
            {
                if (filters[lod] == null)
                {
                    var node = new GameObject("Cord_LOD" + lod); node.transform.SetParent(transform, false);
                    filters[lod] = node.AddComponent<MeshFilter>(); renderers[lod] = node.AddComponent<MeshRenderer>();
                }
                if (renderers[lod] == null) renderers[lod] = filters[lod].GetComponent<MeshRenderer>();
                if (renderers[lod] == null) renderers[lod] = filters[lod].gameObject.AddComponent<MeshRenderer>();
                if (sharedMaterial != null) renderers[lod].sharedMaterial = sharedMaterial;
            }
        }
        /// <summary>Called by the builder, which saves these copies as real .asset meshes.</summary>
        public Mesh[] BakePreviewMeshes()
        {
            RebuildIfNeeded(); var result = new Mesh[3];
            for (int i = 0; i < 3; i++) { result[i] = Instantiate(filters[i].sharedMesh); result[i].name = "FurnitureCord_LOD" + i; result[i].hideFlags = HideFlags.None; }
            return result;
        }
        public void SetPreviewMeshes(Mesh[] savedMeshes)
        {
            if (savedMeshes == null || savedMeshes.Length != 3 || Array.Exists(savedMeshes, mesh => mesh == null))
                throw new ArgumentException("Нужны три сохранённых исходных Mesh провода.");
            previewMeshes = savedMeshes; ReleaseDynamicMeshes();
            for (int i = 0; i < 3; i++) filters[i].sharedMesh = previewMeshes[i];
            initialized = false;
        }
        public bool RebuildIfNeeded()
        {
            if (fixedPart == null || movingPart == null || !gameObject.scene.IsValid()) return false;
            Vector3 a = transform.InverseTransformPoint(FixedWorldAnchor);
            Vector3 b = transform.InverseTransformPoint(MovingWorldAnchor);
            Vector3 outward = transform.InverseTransformDirection(fixedPart.TransformDirection(Vector3.left)).normalized;
            if (initialized && (a-previousFixed).sqrMagnitude < 1e-12f && (b-previousMoving).sqrMagnitude < 1e-12f
                && (outward-previousOutward).sqrMagnitude < 1e-12f) return false;
            EnsureChildren();
            for (int lod = 0; lod < 3; lod++)
            {
                if (dynamicMeshes[lod] == null)
                {
                    dynamicMeshes[lod] = new Mesh { name = "FurnitureCord_Runtime_LOD" + lod, hideFlags = HideFlags.DontSave };
                    dynamicMeshes[lod].MarkDynamic();
                }
                int samples = lod == 0 ? 128 : lod == 1 ? 64 : 24;
                int sides = lod == 2 ? 4 : lod == 1 ? 5 : 6;
                BuildMesh(dynamicMeshes[lod], ref buffers[lod], samples, sides, a, b, outward,
                    lod == 0 ? turns : lod == 1 ? Mathf.Max(1, Mathf.RoundToInt(turns*.6f)) : Mathf.Max(1, Mathf.RoundToInt(turns*.2f)));
                filters[lod].sharedMesh = dynamicMeshes[lod];
            }
            previousFixed = a; previousMoving = b; previousOutward = outward; initialized = true; Revision++;
            return true;
        }
        private void BuildMesh(Mesh mesh, ref Buffers data, int samples, int sides, Vector3 a, Vector3 b, Vector3 outward, int windingCount)
        {
            int sideVertices = (samples + 1)*(sides + 1), capVertices = 2*(sides + 1);
            if (data == null || data.samples != samples || data.sides != sides)
            {
                data = new Buffers { samples = samples, sides = sides, centres = new Vector3[samples + 1],
                    vertices = new Vector3[sideVertices + capVertices], normals = new Vector3[sideVertices + capVertices],
                    tangents = new Vector4[sideVertices + capVertices], uv = new Vector2[sideVertices + capVertices],
                    uv2 = new Vector2[sideVertices + capVertices], triangles = new int[samples*sides*6 + sides*6] };
            }
            Vector3 c1 = a + outward*sideSlack, c2 = b + outward*sideSlack;
            Vector3 priorDirection = Vector3.right, normal = Vector3.up;
            for (int i = 0; i <= samples; i++)
            {
                float t = i/(float)samples, s = 1f-t;
                Vector3 centre = s*s*s*a + 3f*s*s*t*c1 + 3f*s*t*t*c2 + t*t*t*b;
                Vector3 direction = (3f*s*s*(c1-a)+6f*s*t*(c2-c1)+3f*t*t*(b-c2)).normalized;
                if (direction.sqrMagnitude < .5f) direction = priorDirection;
                if (i == 0) normal = Perpendicular(direction);
                else normal = Quaternion.FromToRotation(priorDirection, direction)*normal;
                normal = (normal-direction*Vector3.Dot(normal,direction)).normalized;
                Vector3 binormal = Vector3.Cross(direction,normal).normalized;
                float envelope = Mathf.SmoothStep(0f,1f,Mathf.Min(t/.06f,(1f-t)/.06f));
                float angle = t*windingCount*Mathf.PI*2f;
                data.centres[i] = centre + (Mathf.Cos(angle)*normal + Mathf.Sin(angle)*binormal)*(coilRadius*envelope);
                priorDirection = direction;
            }
            data.centres[0] = a; data.centres[samples] = b;
            float arc = 0f; Vector3 priorTangent = Vector3.right;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 tangent = (data.centres[Mathf.Min(samples,i+1)]-data.centres[Mathf.Max(0,i-1)]).normalized;
                if (tangent.sqrMagnitude < .5f) tangent = priorTangent;
                if (i == 0) normal = Perpendicular(tangent);
                else normal = Quaternion.FromToRotation(priorTangent,tangent)*normal;
                normal = (normal-tangent*Vector3.Dot(normal,tangent)).normalized;
                Vector3 binormal = Vector3.Cross(tangent,normal).normalized;
                if (i > 0) arc += Vector3.Distance(data.centres[i],data.centres[i-1]);
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side/(float)sides*Mathf.PI*2f; int index = i*(sides+1)+side;
                    Vector3 radial = normal*Mathf.Cos(angle)+binormal*Mathf.Sin(angle);
                    Vector3 around = -normal*Mathf.Sin(angle)+binormal*Mathf.Cos(angle);
                    data.vertices[index] = data.centres[i]+radial*tubeRadius; data.normals[index] = radial;
                    data.tangents[index] = new Vector4(around.x,around.y,around.z,1f);
                    data.uv[index] = new Vector2(side/(float)sides,arc/.03f);
                    data.uv2[index] = new Vector2(.02f+.76f*side/sides,.02f+.96f*i/samples);
                }
                priorTangent = tangent;
            }
            int cursor = 0;
            for (int i = 0; i < samples; i++) for (int side = 0; side < sides; side++)
            {
                int x = i*(sides+1)+side, y = x+sides+1;
                data.triangles[cursor++]=x;data.triangles[cursor++]=x+1;data.triangles[cursor++]=y;
                data.triangles[cursor++]=x+1;data.triangles[cursor++]=y+1;data.triangles[cursor++]=y;
            }
            for (int end = 0; end < 2; end++)
            {
                int ring = end == 0 ? 0 : samples*(sides+1), cap = sideVertices+end*(sides+1), centreIndex = cap+sides;
                Vector3 endNormal = end == 0 ? (data.centres[0]-data.centres[1]).normalized : (data.centres[samples]-data.centres[samples-1]).normalized;
                data.vertices[centreIndex]=end==0?a:b;data.normals[centreIndex]=endNormal;data.uv[centreIndex]=Vector2.one*.5f;
                data.uv2[centreIndex]=new Vector2(.9f,end==0?.25f:.75f);data.tangents[centreIndex]=data.tangents[ring];
                for(int side=0;side<sides;side++)
                {
                    int index=cap+side;float angle=side/(float)sides*Mathf.PI*2f;
                    data.vertices[index]=data.vertices[ring+side];data.normals[index]=endNormal;data.tangents[index]=data.tangents[ring+side];
                    data.uv[index]=new Vector2(.5f+.5f*Mathf.Cos(angle),.5f+.5f*Mathf.Sin(angle));
                    data.uv2[index]=data.uv2[centreIndex]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.065f;
                    data.triangles[cursor++]=centreIndex;
                    data.triangles[cursor++]=cap+(end==0?(side+1)%sides:side);
                    data.triangles[cursor++]=cap+(end==0?side:(side+1)%sides);
                }
            }
            mesh.Clear(false);mesh.vertices=data.vertices;mesh.normals=data.normals;mesh.tangents=data.tangents;
            mesh.uv=data.uv;mesh.uv2=data.uv2;mesh.triangles=data.triangles;mesh.RecalculateBounds();
        }
        private static Vector3 Perpendicular(Vector3 tangent)
        { return Vector3.Cross(tangent,Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.95f?Vector3.forward:Vector3.up).normalized; }
        private void ReleaseDynamicMeshes()
        {
            for(int i=0;i<3;i++)
            {
                if(filters!=null&&i<filters.Length&&filters[i]!=null)filters[i].sharedMesh=previewMeshes!=null&&i<previewMeshes.Length?previewMeshes[i]:null;
                if(dynamicMeshes[i]==null)continue;
                if(Application.isPlaying)Destroy(dynamicMeshes[i]);else DestroyImmediate(dynamicMeshes[i]);dynamicMeshes[i]=null;
            }
            initialized=false;
        }
    }
}
