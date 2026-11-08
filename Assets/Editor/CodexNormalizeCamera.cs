#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using GameClub.Player;
using GameClub.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CodexNormalizeCamera
{
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Normalize the camera in Edit Mode.");
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/TestScene.unity")
            throw new InvalidOperationException("Open TestScene before normalizing its camera.");
        var roots = scene.GetRootGameObjects();
        var players = FindInScene<PlayerController2D>(roots);
        if (players.Count != 1) throw new InvalidOperationException("Expected one main-map player.");
        Transform player = players[0].transform;
        var cameras = FindInScene<CinemachineVirtualCamera>(roots);
        var main = cameras.SingleOrDefault(camera => camera.name == "Virtual Camera");
        if (!main) throw new InvalidOperationException("Original follow Virtual Camera was not found.");

        foreach (var area in FindInScene<PuzzleCameraArea>(roots))
        {
            Undo.RecordObject(area, "Replace fixed room cameras with steady player follow");
            area.enabled = false;
            EditorUtility.SetDirty(area);
            if (!area.view || area.view == main) continue;
            Undo.RecordObject(area.view, "Disable obsolete fixed-room framing");
            area.view.Priority = -20;
            area.view.enabled = false;
            EditorUtility.SetDirty(area.view);
        }

        Undo.RecordObject(main, "Restore normal 2D follow lens");
        Undo.RecordObject(main.transform, "Keep follow camera level");
        main.enabled = true;
        main.Priority = 10;
        main.Follow = player;
        main.LookAt = null;
        main.m_Lens.OrthographicSize = 6.8f;
        main.m_Lens.Dutch = 0f;
        main.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
        main.transform.rotation = Quaternion.identity;
        main.transform.position = new Vector3(player.position.x, player.position.y, -10f);
        var body = main.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (!body) body = main.AddCinemachineComponent<CinemachineFramingTransposer>();
        Undo.RecordObject(body, "Tune responsive, steady platformer camera");
        body.enabled = true;
        body.m_TrackedObjectOffset = Vector3.zero;
        body.m_XDamping = 0.18f;
        body.m_YDamping = 0.22f;
        body.m_ZDamping = 0f;
        body.m_LookaheadTime = 0f;
        body.m_LookaheadSmoothing = 0f;
        body.m_LookaheadIgnoreY = true;
        body.m_ScreenX = body.m_ScreenY = 0.5f;
        body.m_CameraDistance = 10f;
        body.m_DeadZoneWidth = 0.04f;
        body.m_DeadZoneHeight = 0.05f;
        body.m_SoftZoneWidth = 0.8f;
        body.m_SoftZoneHeight = 0.8f;
        body.m_BiasX = body.m_BiasY = 0f;
        body.m_CenterOnActivate = true;
        var aim = main.GetCinemachineComponent<CinemachineComposer>();
        if (aim) { Undo.RecordObject(aim, "Disable unused camera aiming"); aim.enabled = false; EditorUtility.SetDirty(aim); }
        var framing = main.GetComponent<GravityCameraFraming>();
        if (!framing) framing = Undo.AddComponent<GravityCameraFraming>(main.gameObject);
        Undo.RecordObject(framing, "Preserve the pendulum interaction in normal follow view");
        framing.enabled = true;
        framing.player = player;
        framing.pendulumBounds = new Rect(26f, -37f, 29f, 17f);
        framing.pendulumFocus = new Vector2(40f, -29f);
        framing.maximumOffset = new Vector2(3.5f, 3.8f);
        framing.offsetSmoothTime = 0.22f;
        main.PreviousStateIsValid = false;
        EditorUtility.SetDirty(framing);
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(main);
        EditorUtility.SetDirty(main.transform);

        foreach (var output in FindInScene<Camera>(roots).Where(camera => camera.CompareTag("MainCamera")))
        {
            Undo.RecordObject(output, "Restore orthographic game view");
            Undo.RecordObject(output.transform, "Keep gameplay view upright");
            output.orthographic = true;
            output.orthographicSize = 6.8f;
            output.transform.rotation = Quaternion.identity;
            output.transform.position = new Vector3(player.position.x, player.position.y, -10f);
            var brain = output.GetComponent<CinemachineBrain>();
            if (brain)
            {
                Undo.RecordObject(brain, "Use a short camera transition");
                brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, 0.25f);
                EditorUtility.SetDirty(brain);
            }
            EditorUtility.SetDirty(output);
            EditorUtility.SetDirty(output.transform);
        }
        Debug.Log("Camera normalized: constant 6.8 follow lens; obsolete room zooms disabled; pendulum remains visible.");
        // The calling map builder owns saving the scene and its undo group.
    }

    private static List<T> FindInScene<T>(GameObject[] roots) where T : Component
    {
        var result = new List<T>();
        foreach (var root in roots) result.AddRange(root.GetComponentsInChildren<T>(true));
        return result;
    }
}
#endif