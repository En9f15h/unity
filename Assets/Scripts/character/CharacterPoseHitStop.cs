using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

// Runs after facing/IK but before SpriteSkin deformation (order 10).
// Only the displayed pose is held; Animator, Photon and gameplay clocks keep advancing.
[DisallowMultipleComponent, DefaultExecutionOrder(5)]
public sealed class CharacterPoseHitStop : MonoBehaviour
{
    struct Pose
    {
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    Transform[] bones;
    Pose[] heldPose, livePose;
    double holdUntil;
    bool poseApplied;
    public bool IsHolding => Time.unscaledTimeAsDouble < holdUntil;
    public int BoneCount => bones != null ? bones.Length : 0;
    public static int FramesForDamage(int damage) => damage <= 0 ? 0 : (int)(damage * 0.6);
    public static double SecondsForDamage(int damage) => FramesForDamage(damage) / 60.0;

    void CacheBones()
    {
        var unique = new HashSet<Transform>();
        foreach (var skin in GetComponentsInChildren<SpriteSkin>(true))
        {
            if (skin.boneTransforms == null) continue;
            foreach (var bone in skin.boneTransforms)
                if (bone != null && bone != transform) unique.Add(bone);
            if (skin.transform != transform) unique.Add(skin.transform);
        }
        bones = new Transform[unique.Count]; unique.CopyTo(bones);
        heldPose = new Pose[bones.Length]; livePose = new Pose[bones.Length];
    }
    void Read(Pose[] poses)
    {
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null) poses[i] = new Pose { position = bones[i].localPosition, rotation = bones[i].localRotation, scale = bones[i].localScale };
    }
    void Apply(Pose[] poses)
    {
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null)
            {
                bones[i].SetLocalPositionAndRotation(poses[i].position, poses[i].rotation);
                bones[i].localScale = poses[i].scale;
            }
    }
    public void HoldForDamage(int damage)
    {
        double duration = SecondsForDamage(damage);
        if (duration <= 0 || !isActiveAndEnabled) return;
        if (bones == null) CacheBones();
        if (!IsHolding)
        {
            RestoreLivePose();
            Read(heldPose);
        }
        // Simultaneous hits use the longest hold instead of adding pauses to the turn.
        holdUntil = System.Math.Max(holdUntil, Time.unscaledTimeAsDouble + duration);
    }
    void RestoreLivePose()
    {
        if (!poseApplied) return;
        Apply(livePose); poseApplied = false;
    }
    void Update() => RestoreLivePose();
    void FixedUpdate() => RestoreLivePose();
    void LateUpdate()
    {
        if (!IsHolding) { RestoreLivePose(); return; }
        Read(livePose);
        Apply(heldPose);
        poseApplied = true;
    }
    void OnDisable() { RestoreLivePose(); holdUntil = 0; }
}
