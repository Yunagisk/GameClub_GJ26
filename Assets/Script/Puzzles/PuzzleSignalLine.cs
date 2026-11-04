using UnityEngine;
namespace GameClub.Puzzles {
 public sealed class PuzzleSignalLine:MonoBehaviour {
  public string flag;public LineRenderer line;
  void Update(){if(!line)return;bool on=PuzzleSession.Instance&&PuzzleSession.Instance.Solved(flag);Color c=on?new Color(.3f,.95f,.8f,.85f):new Color(.25f,.35f,.4f,.35f);line.startColor=line.endColor=c;}
 }
}
