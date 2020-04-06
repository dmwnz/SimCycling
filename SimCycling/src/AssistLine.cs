using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Globalization;

namespace SimCycling
{
    class AssistLine
    {

        List<Tuple<Vector3, float>> points = new List<Tuple<Vector3, float>>();

        public AssistLine(string csvFile)
        {
            IEnumerable<string> lines = File.ReadLines(csvFile);
            CultureInfo cul = new CultureInfo("en-US", false);
            char[] sep = { ',' };
            foreach (var line in lines)
            {
                string[] values = line.Split(sep);
                var x = float.Parse(values[0], cul.NumberFormat);
                var y = float.Parse(values[1], cul.NumberFormat);
                var z = float.Parse(values[2], cul.NumberFormat);
                var p = float.Parse(values[3], cul.NumberFormat);
                points.Add(new Tuple<Vector3, float>(new Vector3(x, y, z), p));
            }
        }

        private int GetIdx(double p)
        {
            int index = 0;
            if (index < 0)
            {
                return 0;
            }
            if (index >= points.Count)
            {
                return points.Count - 1;
            }
            while (index < points.Count - 1 && points[index].Item2 < p)
            {
                index += 1;
            }
            return index;
        }

        public Vector3 GetPoint(double p)
        {
            var idx = GetIdx(p);
            return points[idx].Item1;
        }

        public Tuple<Vector3, Vector3> GetPointAndDirection(double p)
        {
            var idx = GetIdx(p);
            var point = points[idx].Item1;
            Vector3 dir;
            if (idx == 0)
            {
                dir = points[idx + 1].Item1;
                dir = dir - point;
            }
            else
            {
                dir = points[idx - 1].Item1;
                dir = point - dir;
            }
            dir = Vector3.Normalize(dir);
            return new Tuple<Vector3, Vector3>(point, dir);
        }
    }
}
