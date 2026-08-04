using System.IO;
using System.Linq;
using NUnit.Framework;
using Potshot.EditorTools;
using UnityEngine;

namespace Potshot.Tests
{
    /// <summary>Classification tests against Brandon's REAL drawings —
    /// thresholds are only correct if these pass.</summary>
    public class MapImporterTests
    {
        static MapImporter.MapData Load(string file) =>
            MapImporter.Classify(Path.Combine(MapImporter.MapsSourceDir, file));

        static int Count(MapImporter.MapData d, byte type)
        {
            int n = 0;
            for (int x = 0; x < d.cols; x++)
                for (int y = 0; y < d.rows; y++)
                    if (d.cells[x, y] == type) n++;
            return n;
        }

        [Test]
        public void CliffsAndTunnels_HasSolidCliffs_PassableTunnels_TwoSpawns()
        {
            var d = Load("Cliffs and tunnels.png");
            Assert.That(Count(d, MapImporter.Cliff), Is.GreaterThan(100),
                "cliff plateaus should be FILLED, not outlines");
            Assert.That(d.spawnMarkers.Count, Is.EqualTo(2));
            // Pathability: tunnels must connect the map (m1g review — a
            // dead-ended tunnel fails this, not just 'some open cells').
            Assert.That(d.OpenConnectivity(), Is.GreaterThan(0.85f),
                "tunnels/dash carving failed — open space is fragmented");
        }

        [Test]
        public void CliffsAndTunnels_IsNotMirrored()
        {
            // Drawing: one spawn circle top-right, one bottom-left.
            var d = Load("Cliffs and tunnels.png");
            Assert.That(d.spawnMarkers.Any(m => m.x > 0f && m.z > 0f),
                "expected a top-right spawn — y-axis likely flipped");
            Assert.That(d.spawnMarkers.Any(m => m.x < 0f && m.z < 0f),
                "expected a bottom-left spawn — y-axis likely flipped");
        }

        [Test]
        public void ClifsideBattle_HasCliffsGrayCover_TwoSpawns()
        {
            var d = Load("Clifside Battle.png");
            Assert.That(Count(d, MapImporter.Cliff), Is.GreaterThan(80));
            Assert.That(Count(d, MapImporter.Wall), Is.GreaterThan(10),
                "gray rectangles should classify as walls");
            Assert.That(d.spawnMarkers.Count, Is.EqualTo(2));
            Assert.That(d.OpenConnectivity(), Is.GreaterThan(0.85f));
        }

        [Test]
        public void FortsMap_HasWaterBand_BridgesConnectBanks_NoMarkers()
        {
            var d = Load("forts bridges and rivers.png");
            Assert.That(Count(d, MapImporter.Water), Is.GreaterThan(40),
                "river banks should dilate into a water band");
            Assert.That(d.spawnMarkers.Count, Is.EqualTo(0));
            // The river splits the map; only the bridges reconnect it.
            Assert.That(d.OpenConnectivity(), Is.GreaterThan(0.8f),
                "north and south banks must connect via bridges");
        }
    }
}
