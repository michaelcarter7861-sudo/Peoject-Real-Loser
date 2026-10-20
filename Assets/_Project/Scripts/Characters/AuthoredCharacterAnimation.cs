using UnityEngine;
using RealLoserStudio.Tactical.AI;

namespace RealLoserStudio.Tactical.Characters
{
    /// <summary>Plays authored keyframed generic-rig clips on runtime-imported GLB characters.</summary>
    [DisallowMultipleComponent]
    public sealed class AuthoredCharacterAnimation : MonoBehaviour
    {
        private Animation _animation;
        private EnemyAIFSMSystem _ai;
        private Vector3 _lastPosition;
        private float _speed;
        private string _current = string.Empty;

        private void Awake()
        {
            _lastPosition = transform.position;
            _ai = GetComponent<EnemyAIFSMSystem>();
            _animation = GetComponent<Animation>();
            if (_animation == null) _animation = gameObject.AddComponent<Animation>();
            AddClip("PRL_Idle", true);
            AddClip("PRL_Walk", true);
            AddClip("PRL_AimFire", true);
            AddClip("PRL_Reload", false);
            AddClip("PRL_Takedown", false);
            _animation.playAutomatically = false;
            Play("PRL_Idle", 0.18f);
        }

        private void AddClip(string clipName, bool loop)
        {
            AnimationClip clip = Resources.Load<AnimationClip>("PRLAnimations/" + clipName);
            if (clip == null) return;
            if (_animation.GetClip(clipName) == null) _animation.AddClip(clip, clipName);
            AnimationState state = _animation[clipName];
            if (state != null) state.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        }

        private void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            _speed = Vector3.Distance(transform.position, _lastPosition) / dt;
            _lastPosition = transform.position;
            if (_ai != null && _ai.IsNeutralized) { Play("PRL_Takedown", 0.12f); return; }
            if (_ai != null && (_ai.currentState == AIState.CombatCover || _ai.currentState == AIState.Flank))
                Play("PRL_AimFire", 0.15f);
            else if (_speed > 0.18f)
                Play("PRL_Walk", 0.18f);
            else
                Play("PRL_Idle", 0.22f);
        }

        private void Play(string clipName, float fade)
        {
            if (_animation == null || _current == clipName || _animation.GetClip(clipName) == null) return;
            _animation.CrossFade(clipName, fade, PlayMode.StopSameLayer);
            _current = clipName;
        }
    }
}
