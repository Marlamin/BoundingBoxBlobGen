using CASCLib;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using WoWFormatLib.FileProviders;
using WoWFormatLib.FileReaders;

namespace BoundingBoxBlobGen
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("settings.json", true, true).Build();

            if (!File.Exists("listfile.csv"))
                throw new FileNotFoundException("listfile.csv not found");

            CASCConfig.ValidateData = false;
            CASCConfig.ThrowOnFileNotFound = false;
            CASCConfig.UseWowTVFS = false;
            CASCHandler cascHandler;

            if (config["installDir"] != string.Empty && Directory.Exists(config["installDir"]))
            {
                Console.WriteLine("Initializing CASC from local disk with basedir " + config["installDir"] + " and program " + config["program"]);
                cascHandler = CASCHandler.OpenLocalStorage(config["installDir"], config["program"]);
            }
            else
            {
                Console.WriteLine("Initializing CASC from web for program " + config["program"]);
                cascHandler = CASCHandler.OpenOnlineStorage(config["program"], "eu");
            }

            cascHandler.Root.SetFlags(LocaleFlags.enUS, false);

            var splitName = cascHandler.Config.BuildName.Replace("WOW-", "").Split("patch");
            var buildName = splitName[1].Split("_")[0] + "." + splitName[0];

            var casc = new CASCFileProvider();
            casc.InitCasc(cascHandler);
            FileProvider.SetProvider(casc, buildName);
            FileProvider.SetDefaultBuild(buildName);

            var boundingBoxBlobDict = new Dictionary<uint, CAaBox>();

            var filesDone = 0;
            var lastReport = 0;
            foreach (var line in File.ReadAllLines("listfile.csv"))
            {
                var splitLine = line.ToLowerInvariant().Split(";");
                var fileDataID = uint.Parse(splitLine[0]);
                var filename = splitLine[1].ToLowerInvariant();

                if (filesDone > 0 && filesDone % 1000 == 0 && filesDone != lastReport)
                {
                    Console.WriteLine("Processed " + filesDone + " files, at filedataid " + fileDataID);
                    lastReport = filesDone;
                }

                if(!cascHandler.FileExists((int)fileDataID))
                    continue;

                try
                {
                    if (filename.EndsWith(".wmo"))
                    {
                        if (Regex.IsMatch(filename, @"_\d{3}.wmo") || Regex.IsMatch(filename, @"_\d{3}_lod\d{1}.wmo"))
                        {
                            continue;
                        }

                        //Console.WriteLine("[WMO] Loading " + line);
                        var wmoReader = new WMOReader();

                        var wmo = wmoReader.LoadWMO(fileDataID);

                        var boundingBox = new CAaBox();
                        boundingBox.BottomCorner = new C3Vector<float>();
                        boundingBox.BottomCorner.x = wmo.header.boundingBox1.X;
                        boundingBox.BottomCorner.y = wmo.header.boundingBox1.Y;
                        boundingBox.BottomCorner.z = wmo.header.boundingBox1.Z;

                        boundingBox.TopCorner = new C3Vector<float>();
                        boundingBox.TopCorner.x = wmo.header.boundingBox2.X;
                        boundingBox.TopCorner.y = wmo.header.boundingBox2.Y;
                        boundingBox.TopCorner.z = wmo.header.boundingBox2.Z;

                        boundingBoxBlobDict.Add(fileDataID, boundingBox);
                        filesDone++;
                    }
                    else if (filename.EndsWith(".m2"))
                    {
                        //Console.WriteLine("[M2] Loading " + line);
                        var m2Reader = new M2Reader();
                        m2Reader.LoadM2(fileDataID);

                        var boundingBox = new CAaBox();
                        boundingBox.BottomCorner = new C3Vector<float>();
                        boundingBox.BottomCorner.x = m2Reader.model.vertexbox[0].X;
                        boundingBox.BottomCorner.y = m2Reader.model.vertexbox[0].Y;
                        boundingBox.BottomCorner.z = m2Reader.model.vertexbox[0].Z;

                        boundingBox.TopCorner = new C3Vector<float>();
                        boundingBox.TopCorner.x = m2Reader.model.vertexbox[1].X;
                        boundingBox.TopCorner.y = m2Reader.model.vertexbox[1].Y;
                        boundingBox.TopCorner.z = m2Reader.model.vertexbox[1].Z;

                        boundingBoxBlobDict.Add(fileDataID, boundingBox);

                        filesDone++;
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error loading " + line + ": " + e.Message);
                }
                
            }

            File.WriteAllText("blob.json", JsonConvert.SerializeObject(boundingBoxBlobDict, Formatting.Indented));
        }
    }

    public struct CAaBox
    {
        public CAaBox(C3Vector<float> inBottomCorner, C3Vector<float> inTopCorner)
        {
            this.BottomCorner = inBottomCorner;
            this.TopCorner = inTopCorner;
        }

        public override string ToString()
        {
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(14, 2);
            defaultInterpolatedStringHandler.AppendLiteral("Min : ");
            defaultInterpolatedStringHandler.AppendFormatted<C3Vector<float>>(this.BottomCorner);
            defaultInterpolatedStringHandler.AppendLiteral(", Max : ");
            defaultInterpolatedStringHandler.AppendFormatted<C3Vector<float>>(this.TopCorner);
            return defaultInterpolatedStringHandler.ToStringAndClear();
        }

        public C3Vector<float> BottomCorner;

        public C3Vector<float> TopCorner;
    }

    public struct C3Vector<T>
    {
        public T x { readonly get; set; }
        public T y { readonly get; set; }
        public T z { readonly get; set; }

        public override string ToString()
        {
            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(16, 3);
            defaultInterpolatedStringHandler.AppendLiteral("X : ");
            defaultInterpolatedStringHandler.AppendFormatted<T>(this.x);
            defaultInterpolatedStringHandler.AppendLiteral(", Y : ");
            defaultInterpolatedStringHandler.AppendFormatted<T>(this.y);
            defaultInterpolatedStringHandler.AppendLiteral(", Z : ");
            defaultInterpolatedStringHandler.AppendFormatted<T>(this.z);
            return defaultInterpolatedStringHandler.ToStringAndClear();
        }
    }
}
