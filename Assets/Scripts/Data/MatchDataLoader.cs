using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AnimationCreator.Data
{
    public static class MatchDataLoader
    {
        public static MatchDataset Load(string sequenceId, FrdsJointSmoother.Settings? smoothSettings = null)
        {
            var dataset = LoadRaw(sequenceId);
            var settings = smoothSettings ?? FrdsJointSmoother.Settings.Default;
            FrdsJointSmoother.SmoothDataset(dataset, settings);
            return dataset;
        }

        public static MatchDataset LoadRaw(string sequenceId)
        {
            var basePath = Path.Combine(Application.streamingAssetsPath, sequenceId);
            if (!Directory.Exists(basePath))
                throw new DirectoryNotFoundException($"Sequence not found: {basePath}");

            var dataset = new MatchDataset
            {
                Manifest = LoadJson<MatchManifest>(Path.Combine(basePath, "manifest.json"))
            };

            foreach (var team in dataset.Manifest.teams)
                dataset.TeamColors[team.teamId] = team.color;

            foreach (var player in dataset.Manifest.players)
                dataset.PlayersById[player.playerId] = player;

            var eventsFile = LoadJson<MatchEventsFile>(Path.Combine(basePath, "events.json"));
            if (eventsFile?.events != null)
                dataset.Events.AddRange(eventsFile.events);

            var framesPath = Path.Combine(basePath, "frames.jsonl");
            foreach (var line in File.ReadLines(framesPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

            try
            {
                var frame = JsonUtility.FromJson<MatchFrame>(line);
                if (frame != null)
                    dataset.Frames.Add(frame);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Invalid JSON in frames.jsonl near frame {dataset.Frames.Count}: {ex.Message}. Re-run AnimationCreator → Bake demo_sequence from Soccer Pack Clips.",
                    ex);
            }
            }

            if (dataset.Frames.Count == 0)
                throw new InvalidDataException("No frames loaded from frames.jsonl");

            return dataset;
        }

        static T LoadJson<T>(string path) where T : class
        {
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<T>(json);
        }
    }
}
