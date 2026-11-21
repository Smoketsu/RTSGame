// rts/src/Game/Assets/Scripts/Presentation/LinePool.cs
// A reusable pool of world-space LineRenderers for presentation overlays: targeting circles, aim lines, tracers, rings, health
// bars. A caller calls Begin each frame, draws with Line and Circle, and End hides the lines it did not use, so nothing is created
// or destroyed per frame once the pool has grown. Unlit, vertex-coloured (Sprites/Default, always included in builds).
// Part of Rts.Game (presentation only).
using System.Collections.Generic;
using UnityEngine;

namespace Rts.Game.Presentation
{
    /// <summary>Pooled LineRenderers drawn fresh each frame.</summary>
    public sealed class LinePool
    {
        /// <summary>Points per circle. Unit: line vertices.</summary>
        private const int CircleSegments = 40;

        /// <summary>Name prefix of the pooled line objects in the Hierarchy.</summary>
        private readonly string _name;

        /// <summary>Lines created so far. Range: 0 to the most lines one frame used.</summary>
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();

        /// <summary>Lines used since Begin. Range: 0 to the pool size.</summary>
        private int _used;

        /// <summary>Material shared by every line of this pool. Range: null until the first line.</summary>
        private Material _material;

        /// <summary>Creates an empty pool.</summary>
        /// <param name="name">Name prefix of its line objects.</param>
        public LinePool(string name)
        {
            _name = name;
        }

        /// <summary>Starts a frame: every line is free again.</summary>
        public void Begin()
        {
            _used = 0;
        }

        /// <summary>Ends a frame: hides the lines not drawn since Begin.</summary>
        public void End()
        {
            for (int i = _used; i < _lines.Count; i++)
            {
                _lines[i].gameObject.SetActive(false);
            }
        }

        /// <summary>Draws a straight line between two world points.</summary>
        /// <param name="from">Start point.</param>
        /// <param name="to">End point.</param>
        /// <param name="color">Colour, RGBA 0 to 1.</param>
        /// <param name="width">Line width. Unit: world units. Range: 0.01 to 1.</param>
        public void Line(Vector3 from, Vector3 to, Color color, float width)
        {
            LineRenderer line = Next(color, 2, false, width);
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        /// <summary>Draws a horizontal circle.</summary>
        /// <param name="centre">World centre (its height is the circle's height).</param>
        /// <param name="radius">Radius. Unit: world units (tiles).</param>
        /// <param name="color">Colour, RGBA 0 to 1.</param>
        /// <param name="width">Line width. Unit: world units. Range: 0.01 to 1.</param>
        public void Circle(Vector3 centre, float radius, Color color, float width)
        {
            LineRenderer line = Next(color, CircleSegments, true, width);
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                line.SetPosition(i, centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        /// <summary>Destroys every pooled line and the material.</summary>
        public void Dispose()
        {
            foreach (LineRenderer line in _lines)
            {
                if (line != null)
                {
                    Object.Destroy(line.gameObject);
                }
            }
            _lines.Clear();
            if (_material != null)
            {
                Object.Destroy(_material);
            }
        }

        /// <summary>The next free line, created when the pool is too small, set up for this use.</summary>
        /// <param name="color">Colour.</param>
        /// <param name="points">Vertex count.</param>
        /// <param name="loop">True to close the line into a loop.</param>
        /// <param name="width">Line width. Unit: world units.</param>
        private LineRenderer Next(Color color, int points, bool loop, float width)
        {
            if (_used == _lines.Count)
            {
                if (_material == null)
                {
                    _material = new Material(Shader.Find("Sprites/Default"));
                }
                GameObject lineObject = new GameObject(_name + " " + _used);
                LineRenderer created = lineObject.AddComponent<LineRenderer>();
                created.sharedMaterial = _material;
                created.useWorldSpace = true;
                created.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                created.receiveShadows = false;
                _lines.Add(created);
            }
            LineRenderer line = _lines[_used++];
            line.gameObject.SetActive(true);
            line.positionCount = points;
            line.loop = loop;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = color;
            return line;
        }
    }
}
