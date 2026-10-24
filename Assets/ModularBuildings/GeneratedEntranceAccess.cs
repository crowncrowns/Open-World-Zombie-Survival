using System;
using UnityEngine;
namespace ZombieSurvival.ModularBuildings
{
 public sealed class GeneratedEntranceAccess : MonoBehaviour
 {
  [HideInInspector]
  public Terrain terrain;
  [HideInInspector]
  public bool autoFit=false;
  [HideInInspector] public bool uniformGeometry;
  [HideInInspector] public float fixedHeight;
  [TextArea] public string diagnostic;
  [HideInInspector] public bool terrainFitted,fitFailed;
  [HideInInspector] public Matrix4x4 fittedBuildingMatrix;
  [HideInInspector] public float fittedWidth,fittedRiser,fittedTread,fittedLanding;
  [HideInInspector] public Route[] routes=new Route[0];
  [Serializable] public sealed class Route
  {
   public int entrance,steps;
   public bool fitted;
   public float maxRiser,treadDepth,groundContactError;
   public Vector3[] path;
  }
 }
}
