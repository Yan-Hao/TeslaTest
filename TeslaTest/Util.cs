using System;
using System.IO;
using System.Reflection;

namespace TeslaTest
{
    static class Util
    {
        public static string GetPathUri(string filename)
        {
            // get executing application's path
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase);
            path = new Uri(path).LocalPath; // get rid of file:\\
            path = Path.Combine(path, filename);

            // convert path with double backward slashs to URI with forward slash
            UriBuilder uriBuilder = new UriBuilder(path);
            path = Uri.UnescapeDataString(uriBuilder.Path);

            return path;
        }

        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        
    }
}
