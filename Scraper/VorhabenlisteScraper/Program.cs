/*
 * Copyright (c) 2015, Achim 'ahzf' Friedland <achim@offenes-jena.de>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using CsQuery;
using Newtonsoft.Json.Linq;

#endregion

namespace de.offenes_jena.Vorhabenliste.Scraper
{

    public class Program
    {

        #region Data

        private static readonly String VorhabenlisteURL           = "http://www.jena.de/de/stadt_verwaltung/b_rger-services/vorhabenliste/411894?max=50";
        private static readonly String VorhabenlisteBrokenXMLURL  = "http://www.jena.de/de/stadt_verwaltung/b_rger-services/vorhabenliste/411894?template_id=5830&aid=&kid=&max=50&skip=0";

        #endregion

        public static void Main(String[] Arguments)
        {

            #region Download Vorhabenliste

            var HTTPClient  = new HttpClient();
            var regex       = new Regex(@"http://www.jena.de/de/([\d]+)");
            var URLSet      = new HashSet<String>();

            foreach (Match match in regex.Matches(HTTPClient.GetStringAsync(VorhabenlisteBrokenXMLURL).Result))
                URLSet.Add(match.Groups[1].Value);

            #endregion

            #region Prepare JSON

            var VorhabenArray = new JArray();

            var JSON = new JObject(new JProperty("Description",                 "Vorhabenliste der Stadt Jena"),
                                   new JProperty("DataSource", new JObject(
                                       new JProperty("Autor",                       ""),
                                       new JProperty("URL",                         VorhabenlisteURL),
                                       new JProperty("BrokenXMLURL",                VorhabenlisteBrokenXMLURL),
                                       new JProperty("License",                     "none - Assumed amtliches Werk")
                                   )),
                                   new JProperty("DataLiberator", new JObject(
                                       new JProperty("Name",                        "Offenes Jena"),
                                       new JProperty("URL",                         "http://offenes-jena.de"),
                                       new JProperty("License",                     "CC-BY-SA-4.0")
                                   )),
                                   new JProperty("Vorhaben",                    VorhabenArray));

            #endregion

            #region Download and analyse every Vorhaben

            Parallel.ForEach(URLSet, URLPart => {

                Console.WriteLine("Processing... " + "http://www.jena.de/de/" + URLPart + " in thread " + Thread.CurrentThread.ManagedThreadId);

                // Download and parse HTML
                var VorhabenHTML = CQ.Create(HTTPClient.GetStringAsync("http://www.jena.de/de/" + URLPart).Result);

                var Einleitung   = VorhabenHTML["#content_neu_detail"].Children().ToArray();
                var Title        = Einleitung[0].InnerText.Trim();
                var Description  = Einleitung[1].InnerText.Trim();

                var Information  = VorhabenHTML[".tab_panel_helper"].Children().
                                       Select(v => new Tuple<String, String>(v.NodeName, v.InnerText.Trim())).
                                       ToList();

                var Links        = VorhabenHTML[".link_box_left"].Children().
                                       Select(htmlnode => new JObject(
                                                              new JProperty("title", htmlnode.FirstChild.Attributes.Where(a => a.Key == "title").Select(x => x.Value).First().Trim()),
                                                              new JProperty("url",   htmlnode.FirstChild.Attributes.Where(a => a.Key == "href" ).Select(x => x.Value).First().Trim())
                                                          ));

                lock (VorhabenArray)
                {
                    VorhabenArray.Add(new JObject(new JProperty("Title",        Title),
                                                  new JProperty("Description",  Description),

                                                  new JProperty("Links",        new JArray(Links))
                                                 ));
                }

            });

            #endregion

            File.WriteAllText("VorhabenJena.json", JSON.ToString());

            Console.WriteLine("done!");

        }

    }

}
