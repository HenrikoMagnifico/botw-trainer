using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using BotwTrainer.Properties;

namespace BotwTrainer
{
   public class WebResourceFetcher: WebClient
   {
      public string Method { get; set; }
      public string fileName { get; private set; }
      public Uri uri { get; private set; }

      public WebResourceFetcher(string name)
      {
         fileName = name;
         uri = new Uri(string.Format("{0}{1}", Settings.Default.GitUrl, fileName));

         Encoding = Encoding.UTF8;
         CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
         Headers.Add("Cache-Control", "no-cache");
      }

      protected override WebRequest GetWebRequest(Uri address)
      {
         WebRequest webRequest = base.GetWebRequest(address);

         if (!string.IsNullOrWhiteSpace(Method))
            webRequest.Method = Method;

         return webRequest;
      }

      public StringReader Contents()
      {
         return new StringReader(DownloadString(this.uri));
      }

      public bool Exists
      {
         get {
            var oldMethod = Method;
            try {
               Method = "HEAD";
               using (HttpWebResponse response = GetWebRequest(this.uri).GetResponse() as HttpWebResponse)
               {
                  return response.StatusCode == HttpStatusCode.OK;
               }
            } catch (System.Net.WebException ex) {
               return false;
            } finally {
               Method = oldMethod;
            }
         }
      }
   }

   class ResourceDataFile
   {
      private Assembly assembly { get { return Assembly.GetExecutingAssembly(); } }

      private string name;
      private string embeddedPath { get; set; }
      private string executingPath { get; set; }

      public bool EmbeddedExists {
         get { return assembly.GetManifestResourceNames().Contains(embeddedPath); }
      }

      public bool LocalExists {
         get { return File.Exists(executingPath); }
      }

      public bool RemoteExists {
         get { return new WebResourceFetcher(name).Exists; }
      }

      public bool Exists
      {
         get { return (LocalExists || EmbeddedExists || RemoteExists); }
      }

      public ResourceDataFile(String name)
      {
         this.name = name;
         this.embeddedPath = string.Format("{0}.Resources.{1}", assembly.GetName().Name, name);
         this.executingPath = Path.Combine(Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath), name);

      }

      public StringReader ContentsFromEmbedded()
      {
         using (Stream stream = assembly.GetManifestResourceStream(embeddedPath))
         {
            using (StreamReader reader = new StreamReader(stream))
            {
               return new StringReader(reader.ReadToEnd());
            }
         }
      }

      public StringReader ContentsFromWeb()
      {
         return new WebResourceFetcher(name).Contents();
      }

      public StringReader ContentsFromLocalPath()
      {
         return new StringReader(File.OpenText(executingPath).ReadToEnd());
      }

      public StringReader Contents()
      {
         try
         {
            /*
             * Search for offset.yaml in .exe directory first, 
             * followed by embedded resource, and then online.
             */
            
            if (LocalExists) {
               return ContentsFromLocalPath();
            } else if (EmbeddedExists) {
               return ContentsFromEmbedded();
            } else {
               return ContentsFromWeb();
            }
         }
         catch (Exception ex)
         {
            throw new Exception(string.Format("Error loading {0}: {1}: {2}", name, ex.GetType().Name, ex.Message));
         }
      }


   }
}
