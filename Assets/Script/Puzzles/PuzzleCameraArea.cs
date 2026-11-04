using UnityEngine;
using Cinemachine;
namespace GameClub.Puzzles {
 public sealed class PuzzleCameraArea:MonoBehaviour {
  public Rect bounds;public CinemachineVirtualCamera view;public int priority=25;
  void Update(){var s=PuzzleSession.Instance;if(view)view.Priority=s&&s.PlayerBody&&bounds.Contains(s.PlayerBody.position)?priority:-20;}
  void OnDisable(){if(view)view.Priority=-20;}
 }
}
