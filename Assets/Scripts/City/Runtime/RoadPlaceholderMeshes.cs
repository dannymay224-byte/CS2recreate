using System.Collections.Generic;
using City.Core;
using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Тимчасові меші з пласких квадів — щоб система працювала до появи справжніх моделей.
    /// Геометрія та сама, що й у нормальних тайлів: центральний квадрат + "рукави" в бік сусідів.
    /// Коли зʼявляться FBX — просто заповни поля в RoadTileSet, цей клас перестане використовуватись.
    /// </summary>
    public static class RoadPlaceholderMeshes
    {
        public static Mesh Build(RoadPiece piece, float cellSize, float roadWidthRatio = 0.62f)
        {
            float half = cellSize * 0.5f;
            float hw = cellSize * roadWidthRatio * 0.5f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Центр присутній завжди.
            AddRect(verts, uvs, tris, -hw, -hw, hw, hw, cellSize);

            foreach (var dir in BaseDirections(piece))
            {
                switch (dir)
                {
                    case GridDirection.North: AddRect(verts, uvs, tris, -hw,  hw,   hw,  half, cellSize); break;
                    case GridDirection.East:  AddRect(verts, uvs, tris,  hw, -hw,  half,  hw,  cellSize); break;
                    case GridDirection.South: AddRect(verts, uvs, tris, -hw, -half, hw,  -hw,  cellSize); break;
                    case GridDirection.West:  AddRect(verts, uvs, tris, -half, -hw, -hw,  hw,  cellSize); break;
                }
            }

            var mesh = new Mesh { name = "RoadPlaceholder_" + piece };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh[] BuildAll(float cellSize, float roadWidthRatio = 0.62f)
        {
            var all = new Mesh[6];
            for (int i = 0; i < 6; i++)
                all[i] = Build((RoadPiece)i, cellSize, roadWidthRatio);
            return all;
        }

        /// <summary>Напрямки, у які виходить БАЗОВА (неповернена) модель кожного типу.</summary>
        static IEnumerable<GridDirection> BaseDirections(RoadPiece piece)
        {
            switch (piece)
            {
                case RoadPiece.DeadEnd:
                    yield return GridDirection.North;
                    break;
                case RoadPiece.Straight:
                    yield return GridDirection.North;
                    yield return GridDirection.South;
                    break;
                case RoadPiece.Corner:
                    yield return GridDirection.North;
                    yield return GridDirection.East;
                    break;
                case RoadPiece.TJunction:
                    yield return GridDirection.North;
                    yield return GridDirection.East;
                    yield return GridDirection.South;
                    break;
                case RoadPiece.Cross:
                    yield return GridDirection.North;
                    yield return GridDirection.East;
                    yield return GridDirection.South;
                    yield return GridDirection.West;
                    break;
            }
        }

        static void AddRect(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            float xMin, float zMin, float xMax, float zMax, float cellSize)
        {
            int b = verts.Count;

            verts.Add(new Vector3(xMin, 0f, zMin));
            verts.Add(new Vector3(xMin, 0f, zMax));
            verts.Add(new Vector3(xMax, 0f, zMax));
            verts.Add(new Vector3(xMax, 0f, zMin));

            float inv = 1f / cellSize;
            uvs.Add(new Vector2(xMin * inv + 0.5f, zMin * inv + 0.5f));
            uvs.Add(new Vector2(xMin * inv + 0.5f, zMax * inv + 0.5f));
            uvs.Add(new Vector2(xMax * inv + 0.5f, zMax * inv + 0.5f));
            uvs.Add(new Vector2(xMax * inv + 0.5f, zMin * inv + 0.5f));

            tris.Add(b + 0); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b + 0); tris.Add(b + 2); tris.Add(b + 3);
        }
    }
}
