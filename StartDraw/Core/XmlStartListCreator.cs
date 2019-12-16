using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using IOF.XML.V3;

namespace StartDraw {
  public class XmlStartListCreator : IXmlStartListCreator {
    public StartList Create(string eventName, List<StartListEntry> runners) {
      var classes = new List<ClassStart>();
      var doc = new XmlDocument();

      foreach (var grade in runners.GroupBy(p => p.Grade)) {
        foreach (var heat in grade.GroupBy(p => p.Heat)) {
          var firstStart = DateTime.Now;
          var startInterval = 120;
          var runnersInHeat = heat.ToArray();

          var personStarts = new List<PersonStart>();

          for (var i = 0; i < runnersInHeat.Length; i++) {
            var p = runnersInHeat[i];
            var e1 = doc.CreateElement("CompetitionRank");
            e1.InnerText = p.CompetitionRank.ToString();
            var e2 = doc.CreateElement("Group");
            e2.InnerText = p.Group.ToString();
            var e3 = doc.CreateElement("Rank");
            e3.InnerText = p.Rank.ToString();

            personStarts.Add(new PersonStart {
              Organisation = new Organisation {
                Name = p.Federation,
              },
              EntryId = new Id { Value = p.Guid.ToString() },
              Person = new Person {
                Name = new PersonName { Family = p.LastName, Given = p.FirstName },
                Id = new List<Id> { new Id { Type = "IOF ID", Value = p.Id.ToString() } }.ToArray(),
              },
              Start = new List<PersonRaceStart> { new PersonRaceStart {
                StartTime = firstStart.AddSeconds(i * startInterval),
                StartTimeSpecified = true
              } }.ToArray(),
              Extensions = new Extensions {
                Any = new List<XmlElement> {
                  e1,e2,e3
                }.ToArray()
              }
            });
          }

          var c = new ClassStart {
            Class = new Class {
              Name = $"{grade.Key} - Heat {heat.Key}"
            },
            PersonStart = personStarts.ToArray()
          };

          classes.Add(c);
        }
      }

      return new StartList {
        CreateTime = DateTime.Now,
        CreateTimeSpecified = true,
        Creator = "IOF - Start Draw Generator",
        Event = new Event {
          Name = eventName
        },
        IofVersion = "3",
        ClassStart = classes.ToArray()
      };
    }
  }
}