﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dissonance.Demo
{
    public class TriggerVisualizer : MonoBehaviour
    {
        private GameObject _visualisations;
        private BaseCommsTrigger[] _triggers;
        private Material _fillMaterial;
        private Material _outlineMaterial;
        private float _alpha;
        
        public Color Color;

        void Awake()
        {
            _visualisations = new GameObject("Trigger Visualisations");
            _visualisations.transform.parent = gameObject.transform;
            _visualisations.transform.localPosition = Vector3.zero;
            _visualisations.transform.localRotation = Quaternion.identity;

            _fillMaterial = Instantiate(Resources.Load<Material>("TriggerMaterial")) as Material;
            _outlineMaterial = Instantiate(Resources.Load<Material>("TriggerEdgeMaterial")) as Material;

            _triggers = GetComponents<BaseCommsTrigger>();

            var spheres = GetComponents<SphereCollider>();
            foreach (var sphere in spheres)
                CreateCircle(sphere);

            var boxes = GetComponents<BoxCollider>();
            foreach (var box in boxes)
                CreateBox(box);
        }

        void Update()
        {
            if (_triggers.Any(t => t.CanTrigger))
            {
                _visualisations.SetActive(true);

                if (_triggers.Any(t => t.IsColliderTriggered))
                    _alpha = Mathf.Clamp01(_alpha + Time.deltaTime * 4);
                else
                    _alpha = Mathf.Clamp01(_alpha - Time.deltaTime * 4);

                var fillAlpha = Mathf.Lerp(0.7f, 1, _alpha);
                var fillColor = Color.Lerp(new Color(), Color, fillAlpha);
                _fillMaterial.SetColor("_TintColor", fillColor);
                _outlineMaterial.color = Color;
            }
            else
            {
                _visualisations.SetActive(false);
                _alpha = 1;
            }            
        }

        private void CreateCircle(SphereCollider sphere)
        {
            var go = new GameObject("sphere collider");
            go.transform.parent = _visualisations.transform;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            var meshFilter = go.AddComponent<MeshFilter>();

            var mesh = new Mesh();

            const int Vertices = 64;

            var positions = new List<Vector3> { Vector3.zero };
            for (int i = 0; i < Vertices; i++)
            {
                var point = new Vector3(
                    sphere.radius * Mathf.Sin(Mathf.PI * 2 * i / Vertices),
                    0.1f,
                    sphere.radius * Mathf.Cos(Mathf.PI * 2 * i / Vertices));

                positions.Add(point);
            }

            var normals = new List<Vector3>();
            for (int i = 0; i < positions.Count; i++)
                normals.Add(Vector3.up);

            var colors = new List<Color>();
            for (int i = 0; i < positions.Count; i++)
                colors.Add(new Color(1, 1, 1, 0.2f));
            
            var diskIndices = new List<int>();
            for (int i = 0; i < Vertices; i++)
            {
                diskIndices.Add(0);
                diskIndices.Add(i);

                if (i < Vertices - 1)
                    diskIndices.Add(i + 1);
                else
                    diskIndices.Add(1);
            }

            var ringIndices = new List<int>();
            for (int i = 1; i < Vertices; i++)
                ringIndices.Add(i);
            ringIndices.Add(1);

            mesh.vertices = positions.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetIndices(diskIndices.ToArray(), MeshTopology.Triangles, 0);
            mesh.SetIndices(ringIndices.ToArray(), MeshTopology.LineStrip, 1);

            meshFilter.mesh = mesh;
            meshRenderer.materials = new[] { _fillMaterial, _outlineMaterial };
            meshRenderer.receiveShadows = false;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void CreateBox(BoxCollider box)
        {
            var go = new GameObject("box collider");
            go.transform.parent = _visualisations.transform;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var meshRenderer = go.AddComponent<MeshRenderer>();
            var meshFilter = go.AddComponent<MeshFilter>();

            var mesh = new Mesh();

            var min = box.center - box.size * 0.5f;
            var max = box.center + box.size * 0.5f;
            var positions = new List<Vector3>
            {
                new Vector3(min.x, 0.1f, min.z),
                new Vector3(min.x, 0.1f, max.z),
                new Vector3(max.x, 0.1f, max.z),
                new Vector3(max.x, 0.1f, min.z),
            };
            
            var normals = new List<Vector3>();
            for (int i = 0; i < positions.Count; i++)
                normals.Add(Vector3.up);

            var colors = new List<Color>();
            for (int i = 0; i < positions.Count; i++)
                colors.Add(new Color(1, 1, 1, 0.2f));

            var fillIndices = new List<int>
            {
                0, 1, 2,
                2, 3, 0
            };

            var outlineIndices = new List<int>
            {
                0, 1, 2, 3, 0
            };

            mesh.vertices = positions.ToArray();
            mesh.normals = normals.ToArray();
            mesh.colors = colors.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetIndices(fillIndices.ToArray(), MeshTopology.Triangles, 0);
            mesh.SetIndices(outlineIndices.ToArray(), MeshTopology.LineStrip, 1);

            meshFilter.mesh = mesh;
            meshRenderer.materials = new[] { _fillMaterial, _outlineMaterial };
            meshRenderer.receiveShadows = false;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }        
    }
}
