using City.Core;
using UnityEngine;

namespace City.Runtime
{
    /// <summary>
    /// Набір з шести шматків дороги. Поля приймають і звичайний префаб, і FBX-модель напряму:
    /// у Unity імпортована модель це теж GameObject-ассет, окремий префаб робити не обов'язково.
    /// Матеріали, підоб'єкти, колайдери й LOD беруться з самого префаба.
    ///
    /// Вимоги до моделей:
    ///   pivot у центрі блоку, Y=0 на рівні полотна;
    ///   футпринт рівно spanInCells x spanInCells клітинок;
    ///   Straight іде вздовж +Z, Corner з'єднує +Z і +X, TJunction - +Z/+X/-Z, DeadEnd виходить у +Z;
    ///   профіль краю (ширина полотна, висота бордюру) однаковий у всіх шести.
    /// </summary>
    [CreateAssetMenu(menuName = "City/Road Tile Set", fileName = "RoadTileSet")]
    public class RoadTileSet : ScriptableObject
    {
        [Header("Префаби або FBX-моделі (порожні поля замінюються заглушками)")]
        public GameObject standalone;
        public GameObject deadEnd;
        public GameObject straight;
        public GameObject corner;
        public GameObject tJunction;
        public GameObject cross;

        [Header("Розмір")]
        [Tooltip("Скільки клітинок сітки займає сторона одного дорожнього блоку. " +
                 "Реальний розмір у метрах = CityGrid.cellSize * spanInCells.")]
        [Min(1)] public int spanInCells = 1;

        [Tooltip("Масштаб інстансу. 1 = розмір як у префабі.")]
        public float tileScale = 2f;

        [Tooltip("Підйом полотна над землею, щоб не було z-fighting.")]
        public float heightOffset = 0.05f;

        public GameObject Get(RoadPiece piece)
        {
            switch (piece)
            {
                case RoadPiece.Standalone: return standalone;
                case RoadPiece.DeadEnd:    return deadEnd;
                case RoadPiece.Straight:   return straight;
                case RoadPiece.Corner:     return corner;
                case RoadPiece.TJunction:  return tJunction;
                case RoadPiece.Cross:      return cross;
                default: return null;
            }
        }
    }
}
