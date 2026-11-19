using UnityEngine;

namespace TurnCard.Game.Audio
{
    /// <summary>
    /// 全局音效播放器。音频文件放在 Resources/Audio 下，按需加载并缓存。
    /// 每次播放带轻微音高随机，避免连续出牌时听感机械。
    /// </summary>
    public static class Sfx
    {
        private const string ResourceRoot = "Audio/";

        private static AudioSource s_Source;
        private static AudioClip s_Flip;
        private static AudioClip s_Bow;
        private static AudioClip s_Cannon;
        private static AudioClip s_Hit;

        private static AudioSource Source
        {
            get
            {
                if (s_Source == null)
                {
                    var go = new GameObject("[Sfx]");
                    Object.DontDestroyOnLoad(go);
                    s_Source = go.AddComponent<AudioSource>();
                    s_Source.playOnAwake = false;
                    s_Source.spatialBlend = 0f;
                }

                return s_Source;
            }
        }

        private static AudioClip Load(string name, ref AudioClip cache)
        {
            if (cache == null)
            {
                cache = Resources.Load<AudioClip>(ResourceRoot + name);
            }

            return cache;
        }

        private static void Play(AudioClip clip, float pitch, float volume)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource source = Source;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        /// <summary>翻面音。pitch 略低于 1 时翻背面更沉。</summary>
        public static void Flip(bool back = false)
        {
            float pitch = Random.Range(0.96f, 1.04f) * (back ? 0.88f : 1f);
            Play(Load("sfx_flip", ref s_Flip), pitch, 0.8f);
        }

        /// <summary>攻击音效：伤害高用炮弹，低用箭。</summary>
        public static void Attack(int damage)
        {
            if (damage >= 8)
            {
                Play(Load("sfx_cannon", ref s_Cannon), Random.Range(0.95f, 1.02f), 0.85f);
            }
            else
            {
                Play(Load("sfx_bow", ref s_Bow), Random.Range(0.97f, 1.06f), 0.85f);
            }
        }

        /// <summary>命中音。</summary>
        public static void Hit()
        {
            Play(Load("sfx_hit", ref s_Hit), Random.Range(0.94f, 1.06f), 0.9f);
        }
    }
}
