using UnityEngine;

[CreateAssetMenu(fileName = "NewBrickData", menuName = "Brick Data")]
public class BrickData : ScriptableObject
{
    public Material brickMaterial;
    public int pointsValue;
}
