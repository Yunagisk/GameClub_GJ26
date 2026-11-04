using UnityEngine;
namespace GameClub.Puzzles {
 public class PuzzleGateFeedback:MonoBehaviour {
  public PuzzleSmoothGate gate; public TextMesh label;
  void Update(){if(!gate||!label)return;label.text=gate.IsOpen?"OPEN":gate.OpenAmount>.005f?"OPENING":"LOCK";label.color=gate.OpenAmount>.005f?new Color(.35f,.95f,.75f):new Color(.95f,.75f,.4f);}
 }
}
