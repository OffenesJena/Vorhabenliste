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

using org.GraphDefined.Vanaheimr.Illias;
using System.Collections.Concurrent;

#endregion

namespace de.offenes_jena.Vorhabenliste.Scraper
{

    public class Program
    {

        #region Data

        private static readonly String VorhabenlisteURL                     = "http://www.jena.de/de/stadt_verwaltung/b_rger-services/vorhabenliste/411894?max=50";
        private static readonly String VorhabenlisteBrokenXMLURL            = "http://www.jena.de/de/stadt_verwaltung/b_rger-services/vorhabenliste/411894?template_id=5830&aid=&kid=&max=50&skip=0";

        public const String LetzterBeschlussZumVorhaben                     = "Letzter Beschluss zum Vorhaben";
        public const String LetzterPolitischerBeschlussZumVorhaben          = "Letzter politischer Beschluss zum Vorhaben";
        public const String AktuellerBearbeitungsstand                      = "Aktueller Bearbeitungsstand";
        public const String GeplanterZeitpunktDerUmsetzungNächsteSchritte   = "Geplanter Zeitpunkt der Umsetzung / nächste Schritte";
        public const String KostenSoweitBezifferbar                         = "Kosten soweit bezifferbar";
        public const String BetroffenesGebiet                               = "Betroffenes Gebiet";
        public const String SchwerpunktmäßigBetroffeneThemen                = "Schwerpunktmäßig betroffene Themen";
        public const String IstBürgerbeteiligungVorgesehen                  = "Ist Bürgerbeteiligung vorgesehen";

        #endregion

        #region GatherInformation(InfoHash, ref Information, ref i, Thema)

        private static void GatherInformation(Dictionary<String, List<String>>  InfoHash,
                                              ref Tuple<String, String>[]       Information,
                                              ref Int32                         i,
                                              String                            Thema)
        {

            var List = InfoHash.AddAndReturnValue(Thema, new List<String>());
            Information[i] = null;

            while (i+1 < Information.Length && Information[i+1].Item1 != "H3")
            {
                List.Add(Information[i+1].Item2);
                Information[i+1] = null;
                i++;
            }

        }

        #endregion


        public static void Main(String[] Arguments)
        {

            #region Download Vorhabenliste

            var HTTPClient  = new HttpClient();
            var RegExpr     = new Regex(@"http://www.jena.de/de/([\d]+)");
            var URLSet      = new HashSet<String>();
            var Vorhaben    = new ConcurrentDictionary<String, JObject>();

            foreach (Match match in RegExpr.Matches(HTTPClient.GetStringAsync(VorhabenlisteBrokenXMLURL).Result))
                URLSet.Add(match.Groups[1].Value);

            #endregion

            #region Prepare JSON

            var VorhabenArray = new JArray();

            var JSON = new JObject(new JProperty("Description",                 "Vorhabenliste der Stadt Jena"),
                                   new JProperty("DataSource", new JObject(
                                       new JProperty("Autor", new JObject(
                                           new JProperty("Name",                    "Stadt Jena - Team Städtebau & Planungsrecht"),
                                           new JProperty("Ansprechpartner",         "Annette Schwarze-Engel"),
                                           new JProperty("Strasse",                 "Am Anger"),
                                           new JProperty("Hausnummer",              "26"),
                                           new JProperty("PLZ",                     "07743"),
                                           new JProperty("Ort",                     "Jena"),
                                           new JProperty("Telefon",                 "+49 3641 49-5002"),
                                           new JProperty("E-Mail",                  "buergerbeteiligung@jena.de")
                                       )),
                                       new JProperty("URL",                         VorhabenlisteURL),
                                       new JProperty("BrokenXMLURL",                VorhabenlisteBrokenXMLURL),
                                       new JProperty("License",                     "Keine, da amtliches Werk")
                                   )),
                                   new JProperty("DataLiberator", new JObject(
                                       new JProperty("Name",                        "Achim Friedland"),
                                       new JProperty("Organization",                "Offenes Jena"),
                                       new JProperty("URL",                         "http://offenes-jena.de"),
                                       new JProperty("E-Mail",                      "mail@offenes-jena.de"),
                                       new JProperty("GPG-Key", new JObject(
                                           new JProperty("@Id",                         "0xB1EA 6EEA A89A 2896"),
                                           new JProperty("CreationDate",                "2014-08-16"),
                                           new JProperty("Fingerprint",                 "AE0D 5C5C 4EB5 C3F0 683E 2173 B1EA 6EEA A89A 2896"),
                                           new JProperty("URI",                         "http://offenes-jena.de/local/data/Keys/mail@offenes-jena.key")
                                       )),
                                       new JProperty("License",                     "CC-BY-SA-4.0"),
                                       new JProperty("Scraping", new JObject(
                                           new JProperty("Timestamp",               DateTime.Now.ToIso8601()),
                                           new JProperty("SourceCodeURI",           "https://github.com/OffenesJena/Vorhabenliste"),
                                           new JProperty("License",                 "Apache 2.0")
                                       ))
                                   )),
                                   new JProperty("Vorhaben",                    VorhabenArray));

            #endregion

            Parallel.ForEach(URLSet, URLPart => {

                #region Download and analyse every Vorhaben

                Console.WriteLine("Processing... " + "http://www.jena.de/de/" + URLPart + " in thread " + Thread.CurrentThread.ManagedThreadId);

                // Download and parse HTML
                var VorhabenHTML = CQ.Create(HTTPClient.GetStringAsync("http://www.jena.de/de/" + URLPart).Result);

                var Einleitung   = VorhabenHTML["#content_neu_detail"].Children().ToArray();
                var Title        = Einleitung[0].InnerText.Trim();
                var Description  = Einleitung.Skip(1).Select(item => item.InnerText.Trim()).AggregateWith(Environment.NewLine);

                var Links        = VorhabenHTML[".link_box_left"].Children().
                                       Select(htmlnode => new JObject(
                                                              new JProperty("title", htmlnode.FirstChild.Attributes.Where(a => a.Key == "title").Select(x => x.Value).First().Trim()),
                                                              new JProperty("url",   htmlnode.FirstChild.Attributes.Where(a => a.Key == "href" ).Select(x => x.Value).First().Trim())
                                                          ));

                var Information  = VorhabenHTML[".tab_panel_helper"].Children().
                                       Select(v => new Tuple<String, String>(v.NodeName, v.InnerText.Trim())).
                                       ToArray();

                var InfoHash     = new Dictionary<String, List<String>>();

                for (var i = 0; i<Information.Length; i++)
                {

                    if (Information[i].Item1 == "H3")
                    {

                        switch (Information[i].Item2)
                        {

                            case LetzterBeschlussZumVorhaben:
                            case LetzterPolitischerBeschlussZumVorhaben:
                                GatherInformation(InfoHash, ref Information, ref i, LetzterBeschlussZumVorhaben);
                                break;

                            case AktuellerBearbeitungsstand:
                                GatherInformation(InfoHash, ref Information, ref i, AktuellerBearbeitungsstand);
                                break;

                            case GeplanterZeitpunktDerUmsetzungNächsteSchritte:
                                GatherInformation(InfoHash, ref Information, ref i, GeplanterZeitpunktDerUmsetzungNächsteSchritte);
                                break;

                            case KostenSoweitBezifferbar:
                                GatherInformation(InfoHash, ref Information, ref i, KostenSoweitBezifferbar);
                                break;

                            case BetroffenesGebiet:
                                GatherInformation(InfoHash, ref Information, ref i, BetroffenesGebiet);
                                break;

                            case SchwerpunktmäßigBetroffeneThemen:
                                GatherInformation(InfoHash, ref Information, ref i, SchwerpunktmäßigBetroffeneThemen);
                                break;

                            case IstBürgerbeteiligungVorgesehen:
                                GatherInformation(InfoHash, ref Information, ref i, IstBürgerbeteiligungVorgesehen);
                                break;

                            case "-":
                                Information[i] = null;
                                break;

                            default:
                                break;

                        }

                    }

                    else
                        GatherInformation(InfoHash, ref Information, ref i, "Einleitung");

                }

            #endregion

                #region Check and add to JSON

                var RemainingInformation = Information.Where(item => item != null).ToArray();
                if (RemainingInformation.Any())
                {
                    // Should not happen!
                }

                Vorhaben.AddOrUpdate(URLPart,
                                     new JObject(new JProperty("@Id", URLPart),
                                                 new JProperty("Title",        Title),
                                                 new JProperty("Description",  Description),

                                                 InfoHash.Select(item => new JProperty(item.Key, new JArray(item.Value.Where(str => str.IsNotNullOrEmpty())))),

                                                 new JProperty("Links",        new JArray(Links))
                                                ),
                                     (id, json) => json);

                #endregion

            });

            // Resultat sorieren, damit DIFFs besser funktionieren!
            Vorhaben.
                OrderBy(vorhaben => vorhaben.Key).
                ForEach(vorhaben => VorhabenArray.Add(vorhaben.Value));

            File.WriteAllText("VorhabenJena_" + DateTime.Now.ToString("yyyyMMdd") + ".json", JSON.ToString());

            Console.WriteLine("done!");

        }

    }

}
