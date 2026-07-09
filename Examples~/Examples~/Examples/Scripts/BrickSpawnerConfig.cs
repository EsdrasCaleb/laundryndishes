using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BrickSpawnerConfig
{
    public BrickData defaultBrick;
    public List<RareBrickInfo> rareBricks;
}

[Serializable]
public class RareBrickInfo
{
    public BrickData brick;

    [Range(0f, 1f)]
    public float spawnChance;
}
