using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Resonance
{
    /// <summary>Shared step runner for every level: skip key, waits, unlock fanfare, helpers.</summary>
    public abstract class LevelBase : MonoBehaviour
    {
        public static LevelBase Current { get; private set; }
        public int startStep;
        protected bool skip;
        public string StepName { get; private set; } = "";
        public int StepIndex { get; private set; }
        public int StepCount { get; private set; }
        /// <summary>Skip the current step (debug key N, System Check, automated tests).</summary>
        public void SkipStep() => skip = true;
        protected float Eye => Arena.EyeHeight;
        protected float Chest => Arena.EyeHeight - 0.35f;
        protected Transform R => Arena.Root;
        protected Coroutine rockSupply;

        protected abstract Func<IEnumerator>[] Steps();
        protected virtual void OnBegin() { }
        protected virtual void OnEnd() { }

        public IEnumerator Play()
        {
            Current = this;
            var steps = Steps();
            StepCount = steps.Length;
            OnBegin();
            for (int i = Mathf.Clamp(startStep, 0, steps.Length - 1); i < steps.Length; i++)
            {
                skip = false;
                StepIndex = i;
                StepName = GetType().Name + "." + steps[i].Method.Name;
                yield return Run(steps[i]());
            }
            if (Current == this) Current = null;
            if (rockSupply != null) StopCoroutine(rockSupply);
            OnEnd();
        }

        protected virtual void Update()
        {
            if (HandInput.Simulated && SimIn.Down(KeyCode.N)) skip = true;
        }

        protected IEnumerator Run(IEnumerator step)
        {
            while (!skip && step.MoveNext()) yield return step.Current;
            Hud.Progress(-1);
        }

        protected IEnumerator Until(Func<bool> cond) { while (!cond() && !skip) yield return null; }

        protected IEnumerator Wait(float seconds)
        {
            float t = 0;
            while (t < seconds && !skip) { t += Time.deltaTime; yield return null; }
        }

        protected IEnumerator Unlock(string power, string how)
        {
            Hud.Toast(power, how, 4f);
            Sfx.Play(Sound.Unlock, HandInput.Head.position, 1f);
            Build.Burst(HandInput.Head.position + HandInput.Head.forward * 0.6f, Palette.Yellow, 120, 1.5f, 0.02f);
            yield return Wait(1.5f);
        }

        /// <summary>Keeps loose stones available inside a zone (arena-local) during fights and puzzles.</summary>
        protected IEnumerator RockSupply(int count, float zMin = 0.8f, float zMax = 3.6f, float xMax = 2.5f)
        {
            while (true)
            {
                int free = 0;
                foreach (var t in Throwable.All)
                {
                    if (t.isKey || t.locked || t.heavy || t.twistable) continue;
                    Vector3 l = Arena.Local(t.transform.position);
                    if (l.z > zMin && l.z < zMax && Mathf.Abs(l.x) < xMax) free++;
                }
                if (free < count)
                {
                    Vector3 p = new Vector3(UnityEngine.Random.Range(-xMax * 0.6f, xMax * 0.6f), 2.5f, UnityEngine.Random.Range(zMin + 0.6f, zMax - 0.4f));
                    Throwable.Create(R.TransformPoint(p), UnityEngine.Random.Range(0.2f, 0.28f));
                    Build.Burst(R.TransformPoint(p), Palette.Blue, 12, 1f);
                }
                yield return new WaitForSeconds(1.2f);
            }
        }

        protected void StartRockSupply(int count, float zMin = 0.8f, float zMax = 3.6f, float xMax = 2.5f)
        {
            if (rockSupply != null) StopCoroutine(rockSupply);
            rockSupply = StartCoroutine(RockSupply(count, zMin, zMax, xMax));
        }

        protected void StopRockSupply() { if (rockSupply != null) StopCoroutine(rockSupply); rockSupply = null; }

        /// <summary>Remove loose (unsocketed, unheld) stones, e.g. before the next tunnel section slides in.</summary>
        protected static void ClearLooseStones()
        {
            foreach (var t in Throwable.All.ToArray())
                if (t != null && !t.locked && !t.held && !t.anchored && !t.isKey) Destroy(t.gameObject);
        }

        /// <summary>Spawn one creature and wait until it dies.</summary>
        protected IEnumerator Fight(CreatureKind kind, Vector3 local, string title, string hint, Color core)
        {
            var c = Creature.Spawn(kind, R.TransformPoint(local), core);
            bool dead = false;
            c.OnDied += _ => dead = true;
            while (!dead && !skip)
            {
                Hud.Objective(title, hint, new[] { Hud.Check(false, "Neutralise the form") });
                yield return null;
            }
            if (!dead && c != null) Destroy(c.gameObject);
            yield return Wait(1.2f);
        }
    }

    /// <summary>Title menu → play area → the four levels → credits. Handles save, restart and quit-to-title.</summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }
        /// <summary>Set by automated tests before the bootstrap is created: skips the menu and credits.</summary>
        public static bool TestMode;
        public int startLevel = -1, startStep;
        public int CurrentLevel { get; private set; } = -1;
        public bool Finished { get; private set; }
        static readonly Type[] Levels = { typeof(Level0Director), typeof(Level1Tunnels), typeof(Level2Station), typeof(Level3Chamber) };

        IEnumerator Start()
        {
            I = this;
            Save.Apply();
            Hud.Health(Player.I.Health, Player.I.maxHealth);
            PauseWatcher.OnRestart = () => { Save.PendingLevel = Mathf.Max(0, CurrentLevel); Reload(); };
            PauseWatcher.OnQuitToTitle = () => { Save.PendingLevel = -1; Reload(); };
            yield return new WaitUntil(() => HandInput.Head != null);
            yield return null;

            int level;
            bool systemCheck = false;
            if (TestMode) { level = 0; systemCheck = true; }
            else if (Save.PendingLevel >= 0) { level = Save.PendingLevel; Save.PendingLevel = -1; }
            else if (startLevel >= 0) level = startLevel;
            else
            {
                int pick = -99;
                yield return Menu.Title(l => pick = l);
                level = pick;
            }
            if (level == -1) { systemCheck = true; level = 0; }
            if (systemCheck) gameObject.AddComponent<SystemCheck>();

            Arena.I.armed = true;
            if (systemCheck) Arena.I.ForcePlace();
            yield return new WaitUntil(() => Arena.Placed);
            level = Mathf.Clamp(level, 0, Levels.Length - 1);
            PowerSet.Telekinesis = PowerSet.Push = PowerSet.Shield = level > 0;
            PowerSet.Gravity = level > 1;
            PowerSet.Compress = level > 2;

            for (int i = level; i < Levels.Length; i++)
            {
                CurrentLevel = i;
                if (!systemCheck) Save.LastLevel = i;
                var lv = (LevelBase)gameObject.AddComponent(Levels[i]);
                lv.startStep = i == level ? startStep : 0;
                yield return lv.Play();
                Destroy(lv);
                if (!systemCheck) Save.Unlocked = Mathf.Max(Save.Unlocked, Mathf.Min(i + 1, Levels.Length - 1));
            }
            if (!systemCheck) { Save.Completed = true; Save.LastLevel = 0; }
            Finished = true;
            if (TestMode || systemCheck) yield break;
            Hud.Objective("", "", null);
            yield return Menu.Credits();
            Reload();
        }

        public static void Reload()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
