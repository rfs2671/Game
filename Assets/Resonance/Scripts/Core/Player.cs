using UnityEngine;

namespace Resonance
{
    /// <summary>Player health and damage feedback. A red flash in front of the eyes replaces a screen-space vignette.</summary>
    public class Player : MonoBehaviour
    {
        public static Player I { get; private set; }
        public int maxHealth = 5;
        public int Health { get; private set; }
        /// <summary>Training mode: damage never drops health below 1.</summary>
        public bool forgiving = true;
        public System.Action OnDied;

        float invuln, flash;
        GameObject flashQuad;
        Material flashMat;

        void Awake() { I = this; Health = maxHealth; }

        void Start()
        {
            flashQuad = Build.Box("HurtFlash", Mats.Additive(Color.white), null, Vector3.zero, new Vector3(0.6f, 0.4f, 0.001f));
            flashMat = new Material(flashQuad.GetComponent<MeshRenderer>().sharedMaterial);
            flashQuad.GetComponent<MeshRenderer>().sharedMaterial = flashMat;
            flashQuad.SetActive(false);
        }

        public void Heal() { Health = maxHealth; Hud.Health(Health, maxHealth); }

        public void Damage(int amount, Vector3 from)
        {
            if (invuln > 0) return;
            invuln = 0.7f;
            Health -= amount;
            if (forgiving) Health = Mathf.Max(1, Health);
            flash = 1f;
            Sfx.Play(Sound.Hurt, HandInput.Head.position, 1f);
            Hud.Health(Health, maxHealth);
            if (Health <= 0)
            {
                OnDied?.Invoke();
                Health = maxHealth;
                Hud.Health(Health, maxHealth);
            }
        }

        void LateUpdate()
        {
            invuln -= Time.deltaTime;
            if (flash <= 0 || HandInput.Head == null) { if (flashQuad.activeSelf) flashQuad.SetActive(false); return; }
            flash -= Time.deltaTime * 2.5f;
            flashQuad.SetActive(true);
            var h = HandInput.Head;
            flashQuad.transform.SetPositionAndRotation(h.position + h.forward * 0.3f, h.rotation);
            Color c = Palette.Red * Mathf.Clamp01(flash) * 0.7f;
            flashMat.SetColor("_BaseColor", c);
            flashMat.SetColor("_Color", c);
            flashMat.SetColor("_TintColor", c * 0.5f);
        }
    }
}
