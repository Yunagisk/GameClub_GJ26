using UnityEngine;
namespace GameClub.Puzzles {
 public sealed class PuzzleFieldVisual:MonoBehaviour {
  public PuzzleGravityField field; public SpriteRenderer fill; public TextMesh[] arrows;
  void Update(){if(!field)return;bool on=field.Powered(PuzzleSession.Instance);bool up=field.Direction(PuzzleSession.Instance).y>0;Color c=up?new Color(.43f,.72f,.84f,.14f):new Color(.86f,.65f,.39f,.13f);if(!on)c=new Color(.4f,.4f,.5f,.06f);if(fill)fill.color=c;foreach(var t in arrows){if(!t)continue;t.text=on?(up?"↑":"↓"):"OFF";t.color=on?new Color(c.r,c.g,c.b,.65f):new Color(.5f,.5f,.6f,.4f);}}
 }
}
