using UnityEngine;
namespace GameClub.Puzzles {
 public sealed class PuzzleFlagAnd:MonoBehaviour {
  public string[] flags;public string result;
  void Update(){var s=PuzzleSession.Instance;if(!s||flags==null||flags.Length==0||s.Solved(result))return;foreach(var f in flags)if(!s.Solved(f))return;s.Solve(result);}
 }
}
