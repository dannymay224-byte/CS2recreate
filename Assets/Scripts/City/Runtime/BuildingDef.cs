using UnityEngine;

namespace City.Runtime
{
    [CreateAssetMenu(menuName = "City/Building", fileName = "Building")]
    public class BuildingDef : ScriptableObject
    {
        public enum GroundRequirement
        {
            IsFlat,   
            IsWater, 
            IsBlocked   
        }
        public string displayName = "Building";

        [Tooltip("Розмір у клітинках сітки. X — вздовж фасаду, Y — углиб.")]
        public Vector2Int size = Vector2Int.one;

        [Tooltip("Префаб має дивитися фасадом у +Z, pivot по центру футпринта на рівні землі.")]
        public GameObject prefab;
        public GroundRequirement groundRequirement = GroundRequirement.IsFlat;

        [Tooltip("Рандомний поворот навколо Y на основі координат (зберігається після перезапуску).")]
        public bool allowRandomRotation;

        [Header("Вимоги до місця")]
        [Tooltip("Чи обов'язково мати дорогу поруч.")]
        public bool requiresRoad = true;

        [Tooltip("На скільки клітинок навколо шукати дорогу. 1 = дорога має торкатися будівлі.")]
        [Min(1)] public int roadSearchRing = 1;

        [Tooltip("Повертати будівлю фасадом до знайденої дороги.")]
        public bool faceRoad = true;

        public int cost;
    }
}
