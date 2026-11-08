using UnityEngine;
namespace GameClub.Puzzles {
 public sealed class PuzzleStageHint:MonoBehaviour {
  public PuzzleRoom room;public string firstFlag,secondFlag;[TextArea] public string initial,afterFirst,afterSecond;
  void Update(){var s=PuzzleSession.Instance;if(!s||!room)return;room.objective=s.Solved(secondFlag)?afterSecond:s.Solved(firstFlag)?afterFirst:initial;}
 }
}
