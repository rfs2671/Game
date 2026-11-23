using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Resonance
{
    /// <summary>
    /// Level 0, The Quarry: the training sequence from the design doc.
    /// Walk → touch → grab → throw → push → shield → block → return fire → door puzzle →
    /// three encounters (walker, charger, thrower) → collapse → hold the passage → escape.
    /// All positions are arena-local metres: x right, y up from the floor, z forward. The room is |x|,|z| ≤ 1.
    /// </summary>
    public class Level0Director : LevelBase
    {
        protected override Func<IEnumerator>[] Steps() => new Func<IEnumerator>[]
        {
            Fracture, Walk, Touch, Grab, Throw, Push, Shield, Block, ReturnFire, Door,
            WalkerFight, ChargerFight, ThrowerFight, Collapse, Escape, Complete
        };

        protected override void OnBegin()
        {
            Hud.Accent(Palette.Yellow);
            Quarry.Create(R);
            if (startStep > 0) Quarry.I.Reveal();
            PowerSet.Telekinesis = startStep > 3;
            PowerSet.Push = startStep > 5;
            PowerSet.Shield = startStep > 6;
        }

        T Keep<T>(T c) where T : Component => c;

        // ---------------------------------------------------------------- steps
        IEnumerator Fracture()
        {
            Hud.Section("THE QUARRY");
            Hud.Objective("", "", null);
            Quarry.I.Reveal();
            yield return new WaitForSeconds(2.4f);
            Hud.Say("Your hands aren't weapons. They're instruments.", 3.5f);
            Hud.Say("Learn to control what answers them.", 3.5f);
            yield return new WaitForSeconds(5f);
        }

        IEnumerator Walk()
        {
            var ped = Build.Box("Pedestal", Mats.Lit(Palette.StoneLight, Palette.Blue * 0.1f), R,
                new Vector3(0, (Chest - 0.08f) * 0.5f, 0.75f), new Vector3(0.32f, Chest - 0.08f, 0.32f), true);
            Build.Box("Band", Mats.Glow(Palette.Blue), ped.transform, new Vector3(0, 0.45f, -0.51f), new Vector3(1.02f, 0.02f, 0.02f));
            var mineral = new GameObject("Mineral");
            mineral.transform.SetParent(R, false);
            mineral.transform.localPosition = new Vector3(0, Chest + 0.03f, 0.75f);
            for (int i = 0; i < 4; i++)
                Build.Box("Spike", Mats.Lit(Palette.White, Palette.Blue * 1.8f, 0.9f), mineral.transform, Vector3.zero,
                    new Vector3(0.035f, 0.14f - i * 0.02f, 0.035f), false, Quaternion.Euler(i * 20 - 30, i * 70, i * 15 - 20));
            var glow = Build.Particles("MineralGlow", mineral.transform, Palette.Blue, 0.05f, 0.05f, 1f, 20f, 0.05f, true);
            glow.Play();
            mineral.AddComponent<Touchable>();

            Hud.Objective("WALK", "Step forward to the pedestal.", null);
            yield return Until(() => Arena.HeadLocal.z > 0.3f && Mathf.Abs(Arena.HeadLocal.x) < 0.7f);
        }

        IEnumerator Touch()
        {
            var t = Find.One<Touchable>();
            bool touched = t == null;
            if (t != null) t.OnTouched += () => touched = true;
            Hud.Objective("TOUCH", "Reach out and touch the glowing mineral.", null);
            yield return Until(() => touched);
            Hud.Say("It answered you. Good.", 2.5f);
            yield return new WaitForSeconds(1f);
        }

        IEnumerator Grab()
        {
            PowerSet.Telekinesis = true;
            Hud.Section("TRAINING 01");
            Hud.Toast("TELEKINESIS", "Grab and throw objects", 3f);
            Vector3[] spots = { new Vector3(-0.7f, 0, 2.2f), new Vector3(0, 0, 2.6f), new Vector3(0.7f, 0, 2.2f) };
            foreach (var s in spots)
            {
                Build.Box("Slab", Mats.Lit(Palette.Stone, Palette.Blue * 0.05f), R, s + new Vector3(0, 0.4f, 0), new Vector3(0.5f, 0.8f, 0.5f), true);
                Throwable.Create(R.TransformPoint(s + new Vector3(0, 0.95f, 0)), 0.22f);
            }
            Hud.Objective("TELEKINESIS", "Aim at a stone and pinch your thumb and index finger to lift it.", null);
            yield return Until(() => HandPowers.All.Exists(h => h.Holding));
        }

        IEnumerator Throw()
        {
            int broken = 0;
            Vector3[] spots = { new Vector3(-1.3f, Eye, 5f), new Vector3(0, Eye + 0.3f, 5.6f), new Vector3(1.3f, Eye, 5f) };
            foreach (var s in spots) Crystal.Create(R.TransformPoint(s)).OnBroken += _ => broken++;
            StartRockSupply(3);
            while (broken < 3 && !skip)
            {
                Hud.Objective("TELEKINESIS", "Move your hand toward a crystal and release the pinch to throw.",
                    new[] { Hud.Check(false, $"Shatter the crystals ({broken}/3)") });
                Hud.Progress(broken / 3f);
                yield return null;
            }
        }

        IEnumerator Push()
        {
            PowerSet.Push = true;
            Hud.Toast("FORCE PUSH", "Open your palm and thrust", 3f);
            var stack = new List<Throwable>();
            var start = new List<Vector3>();
            Vector3[] spots =
            {
                new Vector3(-0.4f, 0.2f, 2.9f), new Vector3(0, 0.2f, 2.9f), new Vector3(0.4f, 0.2f, 2.9f),
                new Vector3(-0.2f, 0.6f, 2.9f), new Vector3(0.2f, 0.6f, 2.9f), new Vector3(0, 1.0f, 2.9f)
            };
            foreach (var s in spots)
            {
                var t = Throwable.Create(R.TransformPoint(s), 0.36f, false, 0);
                t.transform.rotation = R.rotation;
                stack.Add(t);
                start.Add(t.transform.position);
            }
            yield return new WaitForSeconds(0.8f);
            int moved = 0;
            while (moved < 3 && !skip)
            {
                moved = 0;
                for (int i = 0; i < stack.Count; i++)
                    if (stack[i] != null && Vector3.Distance(stack[i].transform.position, start[i]) > 0.6f) moved++;
                Hud.Objective("FORCE PUSH", "Open your palm toward the stack and thrust it forward.",
                    new[] { Hud.Check(moved >= 3, "Knock the stack down") });
                yield return null;
            }
            Hud.Say("Push with both hands at once for a heavier wave.", 3.5f);
        }

        IEnumerator Shield()
        {
            PowerSet.Shield = true;
            Hud.Toast("ROCK SHIELD", "Raise an open palm and hold it still", 3f);
            float held = 0;
            while (held < 1.2f && !skip)
            {
                held = HandPowers.All.Exists(h => h.ShieldUp) ? held + Time.deltaTime : 0;
                Hud.Objective("ROCK SHIELD", "Raise an open palm toward the quarry and hold it still.", null);
                Hud.Progress(held / 1.2f);
                yield return null;
            }
        }

        Launcher launcher;

        IEnumerator Block()
        {
            launcher = Keep(Launcher.Spawn(Arena.W(0, 0, 6.5f), R.rotation));
            int blocked = 0;
            Action onBlock = () => blocked++;
            Projectile.OnBlocked += onBlock;
            Hud.Say("Something in the quarry has noticed you.", 3f);
            yield return new WaitForSeconds(2f);
            launcher.firing = true;
            while (blocked < 3 && !skip && launcher != null)
            {
                Hud.Objective("ROCK SHIELD", "Block the machine's projectiles with your shield.",
                    new[] { Hud.Check(false, $"Block ({blocked}/3)") });
                Hud.Progress(blocked / 3f);
                yield return null;
            }
            Projectile.OnBlocked -= onBlock;
        }

        IEnumerator ReturnFire()
        {
            while (launcher != null && launcher.Alive && !skip)
            {
                Hud.Objective("ROCK SHIELD", "Catch a shot, then thrust your palm through the shield to return the shards.",
                    new[] { Hud.Check(false, "Destroy the machine") });
                yield return null;
            }
            if (launcher != null && launcher.Alive) Destroy(launcher.gameObject);
            Hud.Say("Shields are made to be thrown.", 3f);
            yield return new WaitForSeconds(1.5f);
        }

        IEnumerator Door()
        {
            var door = StoneDoor.Spawn(Arena.W(0, 0, 4.6f), R.rotation);
            Vector3[] spots = { new Vector3(-1.7f, 0, 3.2f), new Vector3(1.7f, 0, 3.2f) };
            foreach (var s in spots)
            {
                Build.Box("Plinth", Mats.Lit(Palette.Stone, Palette.Yellow * 0.1f), R, s + new Vector3(0, 0.25f, 0), new Vector3(0.6f, 0.5f, 0.6f), true);
                Throwable.Create(R.TransformPoint(s + new Vector3(0, 0.75f, 0)), 0.3f, true).damage = 0;
            }
            Hud.Say("A sealed door. It wants something back.", 3f);
            while (!door.Open && !skip)
            {
                string hint = door.Ready ? "Both keys are set. Force Push the door open."
                    : "Lift the yellow keystones into the sockets on the door frame. Move your hand closer or further to adjust distance.";
                Hud.Objective("THE DOOR", hint, new[]
                {
                    Hud.Check(door.Ready, $"Place the keystones ({door.Filled}/2)"),
                    Hud.Check(false, "Push the door open")
                });
                yield return null;
            }
            yield return new WaitForSeconds(2f);
        }

        IEnumerator FightL0(CreatureKind kind, string title, string hint, Color core)
        {
            Hud.Section("TRAINING 02");
            if (rockSupply == null) StartRockSupply(4);
            Hud.Say("The cracks are waking up.", 2.5f);
            yield return Fight(kind, new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, 7f), title, hint, core);
        }

        IEnumerator WalkerFight() => FightL0(CreatureKind.Walker, "EMERGENT FORM",
            "Throw stones at the creature's glowing core. Push it back if it gets close.", Palette.Yellow);

        IEnumerator ChargerFight() => FightL0(CreatureKind.Charger, "ARMOURED FORM",
            "Stones glance off its armour. Push it back as it charges, then hit the exposed core.", Palette.Yellow);

        IEnumerator ThrowerFight() => FightL0(CreatureKind.Thrower, "DISTANT FORM",
            "Raise your shield to catch its rocks, then thrust to return fire.", Palette.Yellow);

        IEnumerator Collapse()
        {
            StopRockSupply();
            Hud.Section("THE QUARRY");
            Quarry.I.Collapse();
            Hud.Say("The quarry is closing. You won't beat what's coming. Hold the way open.", 4f);
            StoneHand.Spawn(Quarry.I.transform);
            if (Arena.HeadLocal.z > 0.35f)
            {
                Hud.Objective("STEP BACK", "Step back to the centre of your space.", null);
                yield return Until(() => Arena.HeadLocal.z < 0.15f);
            }
            var passage = Passage.Spawn(Quarry.I.transform);
            while (passage.Progress < 1f && !skip)
            {
                Hud.Objective("HOLD THE PASSAGE", passage.Holding ? "Hold steady…" : "Raise BOTH open palms toward the walls and hold them still.",
                    new[] { Hud.Check(false, "Keep the passage open") });
                Hud.Progress(passage.Progress);
                yield return null;
            }
            Sfx.Play(Sound.Chime, HandInput.Head.position, 1f);
        }

        IEnumerator Escape()
        {
            Hud.Objective("ESCAPE", "Walk forward into the light.", null);
            yield return Until(() => Arena.HeadLocal.z > 0.6f);
            Build.Burst(HandInput.Head.position + HandInput.Head.forward * 0.6f, Palette.White, 300, 4f, 0.06f);
            Sfx.Play(Sound.Thump, HandInput.Head.position, 1f);
        }

        IEnumerator Complete()
        {
            Hud.Toast("LEVEL 0 COMPLETE", "Unlocked: Telekinesis · Force Push · Rock Shield", 5f);
            Sfx.Play(Sound.Unlock, HandInput.Head.position, 1f);
            Hud.Objective("", "", null);
            yield return Wait(4f);
        }
    }
}
