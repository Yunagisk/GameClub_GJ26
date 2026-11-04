using UnityEngine;
using GameClub.Player;
namespace GameClub.Puzzles {
 public sealed class PuzzleGravityIndicator:MonoBehaviour {
  public GravityBody2D body;public TextMesh label;
  void Update(){if(!body||!label)return;label.text=body.isGravityFlipped?"↑":"↓";label.color=body.isGravityFlipped?new Color(.5f,.95f,1f):new Color(1f,.78f,.4f);label.transform.rotation=Quaternion.identity;}
 }
}
