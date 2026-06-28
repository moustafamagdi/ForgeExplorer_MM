using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ForgeExplorer.Models
{
    internal class Credentials
    {
        public string TokenInternal { get; private set; }

        private Credentials() { }

        public static Credentials GetFromAdWebServices()
        {
            string revitPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            if (string.IsNullOrWhiteSpace(revitPath))
            {
                return null;
            }

            string ssonetPath = Path.Combine(revitPath, "SSONET.dll");
            if (!File.Exists(ssonetPath))
            {
                Debug.WriteLine($"SSONET.dll was not found at {ssonetPath}.");
                return null;
            }

            Assembly ssonet = Assembly.LoadFrom(ssonetPath);
            Type adWebServicesBaseType = ssonet.GetTypes()
                .FirstOrDefault(q => q.FullName == "Autodesk.Revit.AdWebServicesBase");

            object adWebServicesBase = adWebServicesBaseType
                ?.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);

            string token = adWebServicesBase
                ?.GetType()
                .GetMethod("GetOAuth2AccessToken", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(adWebServicesBase, null) as string;

            if (string.IsNullOrWhiteSpace(token))
            {
                Debug.WriteLine("Autodesk OAuth token was not available. Make sure the user is signed in to Autodesk from Revit 2024.");
                return null;
            }

            return new Credentials { TokenInternal = token };
        }
    }
}
