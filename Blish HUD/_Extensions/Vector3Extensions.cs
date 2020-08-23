using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gw2Sharp.Models;
using Microsoft.Xna.Framework;

namespace Blish_HUD {
    public static class Vector3Extensions {

        /// <summary>
        /// Converts Gw2Sharp's left-handed <see cref="Coordinates3"/> to XNA's right-handed <see cref="Vector3"/>.
        /// </summary>
        public static Vector3 ToXnaVector3(this Coordinates3 vector) {
            return new Vector3((float)vector.X, (float)vector.Z, (float)vector.Y);
        }

        public static string ToRoundedString(this Vector3 vector) {
            return $"X: {vector.X:0,0} Y: {vector.Y:0,0} Z: {vector.Z:0,0}";
        }

        public static List<Vector3> DouglasPeucker(this List<Vector3> vectors, float error = 0.2f) {
            if (vectors.Count < 3) {
                return new List<Vector3>(vectors);
            }

            // indices to points to keep
            var keep = new ConcurrentBag<int> {
                0,
                vectors.Count - 1
            };
            Recursive(vectors, error, 0, vectors.Count - 1, keep);
            List<int> keepList = keep.ToList();
            keepList.Sort();
            return keepList.Select(i => vectors[i]).ToList();
        }

        private static void Recursive(IReadOnlyList<Vector3> vectors, float error, int first, int last, ConcurrentBag<int> keep) {
            if (last - first + 1 < 3) {
                return;
            }

            var vFirst = vectors[first];
            var vLast = vectors[last];

            var lastToFirst = vLast - vFirst;
            float length = lastToFirst.Length();
            float maxDist = error;
            int split = 0;

            for (int i = first + 1; i < last; i++) {
                var v = vectors[i];
                // distance to line vFirst -> vLast
                float dist = Vector3.Cross(vFirst - v, lastToFirst).Length() / length;

                if (dist < maxDist) continue;

                maxDist = dist;
                split = i;
            }

            if (split == 0) return;

            keep.Add(split);
            var tasks = new Task[2];
            tasks[0] = Task.Run(() => Recursive(vectors, error, first, split, keep));
            tasks[1] = Task.Run(() => Recursive(vectors, error, split, last, keep));
            foreach (var task in tasks) {
                task.Wait();
            }
        }
    }
}
