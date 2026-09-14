using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeyLearner.Studio;
using UnityEngine;

namespace KeyLearner.Unity
{
    // Explicit preview-only UX replay. Never installs hooks or synthesizes OS keys.
    public sealed class PreviewSession : MonoBehaviour
    {
        Suite suite; string output; readonly List<string> passed = new List<string>();
        bool failed; int lostFocus, gainedFocus;
        public static void Attach(Suite suite)
        {
            if (!suite.Services.Preview || !suite.Services.Options.Has("--ux-verify"))
                return;
            var probe = suite.gameObject.AddComponent<PreviewSession>();
            probe.suite = suite;
            probe.output = Path.GetFullPath(suite.Services.Options.Value("--ux-verify"));
            Directory.CreateDirectory(probe.output);
            probe.StartCoroutine(probe.Run());
        }
        void Check(bool condition, string name)
        {
            if (condition)
            {
                passed.Add(name);
                Debug.Log("UX_PASS " + name);
                return;
            }
            failed = true;
            Debug.LogError("UX_FAIL " + name);
            Write("failed", name);
            suite.Quit();
        }
        void Write(string phase, string error = "")
        {
            var report = new
            {
                phase,
                error,
                passed,
                lostFocus,
                gainedFocus,
                focused = Application.isFocused,
                suite = suite.PreviewState,
                parent = suite.PreviewParent.PreviewState
            };
            string path = Path.Combine(output, "ux-state.json"), temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(report));
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return null;
            yield return null;
        }
        void ParentKey(int key)
        {
            var parent = suite.PreviewParent;
            parent.Key(new KeyEvent(key, true, suite.Services.Now));
            parent.Key(new KeyEvent(key, false, suite.Services.Now));
        }
        IEnumerator Run()
        {
            yield return null;
            yield return null;
            suite.Select(KeyLearner.Studio.PlayMode.SmashGarden);
            yield return null;
            var smash = suite.CurrentGame;
            suite.PreviewGameKey(27);
            Check(!suite.Picking && ReferenceEquals(smash, suite.CurrentGame), "single Escape keeps Smash Garden and shows parent reminder");
            if (failed)
                yield break;
            yield return Shot("smash-escape-reminder");
            suite.Select(KeyLearner.Studio.PlayMode.Subitizing);
            yield return null;
            var dots = suite.CurrentGame;
            suite.PreviewGameKey(27);
            Check(!suite.Picking && ReferenceEquals(dots, suite.CurrentGame), "single Escape keeps Dot Pop");
            if (failed)
                yield break;
            suite.Select(KeyLearner.Studio.PlayMode.HowManyNow);
            yield return null;
            suite.PreviewGameKey(27);
            Check(suite.Picking, "single Escape follows math Back route");
            if (failed)
                yield break;
            suite.OpenPicker();
            yield return Shot("picker-all");
            suite.PreviewPickerKey(9);
            yield return null;
            Check(suite.PreviewState.Contains("filter=1"), "picker Tab category");
            if (failed)
                yield break;
            yield return Shot("picker-explore");
            suite.PreviewPickerKey(39);
            suite.PreviewPickerKey(13);
            yield return null;
            Check((int)suite.Services.Settings.Mode == 4 && !suite.Picking, "picker Right and Enter choose Letter Racer");
            if (failed)
                yield break;
            suite.OpenPicker();
            suite.PreviewPickerKey(9);
            suite.PreviewPickerKey(9);
            suite.PreviewPickerKey(9);
            for (int i = 0; i < 7; i++)
                suite.PreviewPickerKey(39);
            Check(suite.PreviewState.Contains("page=1"), "picker keyboard reaches second page");
            if (failed)
                yield break;
            yield return Shot("picker-second-page");
            suite.PreviewPickerKey(13);
            suite.PreviewStudio(true);
            yield return null;
            Check(suite.PreviewState.Contains("studio=True"), "Parent Studio opens");
            if (failed)
                yield break;
            var parent = suite.PreviewParent;
            for (int tab = 0; tab < 9; tab++)
            {
                parent.PreviewTab(tab);
                yield return null;
                yield return Shot("studio-" + tab);
                Check(parent.PreviewState.StartsWith("tab=" + tab + " "), "Parent Studio tab " + tab);
                if (failed)
                    yield break;
            }
            parent.PreviewCredits();
            yield return Shot("studio-credits");
            parent.PreviewTab(0);
            yield return null;
            bool gentle = suite.Services.Settings.GentleMotion;
            ParentKey(40);
            ParentKey(40);
            ParentKey(40);
            ParentKey(39);
            Check(suite.Services.Settings.GentleMotion != gentle, "parent arrows edit gentle motion");
            if (failed)
                yield break;
            suite.PreviewStudio(false);
            yield return null;
            var saved = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(suite.Services.Store.Root, "settings.json")));
            Check(saved.GentleMotion == suite.Services.Settings.GentleMotion, "Parent Studio saves isolated profile");
            if (failed)
                yield break;
            suite.PreviewStudio(true);
            parent.PreviewEdit();
            suite.PreviewFocusLoss();
            Check(parent.PreviewState.Contains("editing=False") && !suite.Services.Keys.Keys.Any(), "shared focus reset clears text editor and held keys");
            if (failed)
                yield break;
            if (suite.Services.Options.Has("--native-focus"))
            {
                double until = Time.unscaledTimeAsDouble + 15;
                while (!Application.isFocused && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(Application.isFocused, "native player begins focused");
                if (failed)
                    yield break;
                parent.PreviewEdit();
                int beforeLost = lostFocus, beforeGained = gainedFocus;
                Write("await-native-focus-loss");
                until = Time.unscaledTimeAsDouble + 20;
                while (lostFocus == beforeLost && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(lostFocus > beforeLost, "actual minimized window delivered focus loss");
                if (failed)
                    yield break;
                Write("await-native-focus-return");
                until = Time.unscaledTimeAsDouble + 20;
                while ((!Application.isFocused || gainedFocus == beforeGained) && Time.unscaledTimeAsDouble < until)
                    yield return null;
                Check(Application.isFocused && gainedFocus > beforeGained, "actual restored window delivered focus return");
                if (failed)
                    yield break;
                Check(parent.PreviewState.Contains("editing=False"), "native focus loss canceled unfinished parent edit");
                if (failed)
                    yield break;
                yield return Shot("studio-focus-recovered");
            }
            suite.PreviewStudio(false);
            Check(suite.PreviewState.Contains("studio=False"), "Parent Studio closes to play");
            if (failed)
                yield break;
            Write("complete");
            yield return null;
            suite.Quit();
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused)
                gainedFocus++;
            else
                lostFocus++;
        }
    }
}
