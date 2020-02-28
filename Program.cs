using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.VisualBasic.FileIO;

namespace BestPres
{
    class Program
    {
        class AttendeeInfo
        {
            public List<string> conflicts;
            public string vote;

            public AttendeeInfo(List<string> l)
            {
                conflicts = l;
                vote = null;
            }
        }

        class PresentationInfo
        {
            public string title;
            public uint votes;

            public PresentationInfo(string s)
            {
                title = s;
                votes = 0;
            }
        }

        // Dictionary mapping from confirmation code to conflicts.
        static Dictionary<string, AttendeeInfo> ReadAttendees(string attendeePath)
        {
            Dictionary<string, AttendeeInfo> attendees = new Dictionary<string, AttendeeInfo>();
            using ( TextFieldParser parser = new TextFieldParser(attendeePath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.Delimiters = new string[] { "," };
                parser.HasFieldsEnclosedInQuotes = true;
                // Throw away the title row.
                parser.ReadFields();
                while (!parser.EndOfData)
                {
                    string[] details = parser.ReadFields();
                    if (details.Length < 5)
                    {
                        throw new Exception("Missing data in row " + attendees.Count + "of attendees.");
                    }
                    List<string> attendeeConflicts = new List<string>();
                    for (int i = 5; i < details.Length; i++)
                    {
                        if (details[i] != "")
                        {
                            attendeeConflicts.Add(details[i]);
                        }
                    }
                    attendees.Add(details[4], new AttendeeInfo(attendeeConflicts));
                }
            }
            return attendees;
        }

        // Dictionary mapping from confirmation code to conflicts.
        static Dictionary<string, PresentationInfo> ReadPresentations(string presentationPath)
        {
            Dictionary<string, PresentationInfo> presentations = new Dictionary<string, PresentationInfo>();
            using (TextFieldParser parser = new TextFieldParser(presentationPath))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.Delimiters = new string[] { "," };
                parser.HasFieldsEnclosedInQuotes = true;
                // Throw away the title row.
                parser.ReadFields();
                while (!parser.EndOfData)
                {
                    string[] details = parser.ReadFields();
                    if (details.Length < 2)
                    {
                        throw new Exception("Missing data in row " + presentations.Count + "of presentations.");
                    }
                    presentations.Add(details[0], new PresentationInfo(details[1]));
                }
            }
            return presentations;
        }

        static void Main(string[] args)
        {
            string attendeePath = null;
            string presentationsPath = null;
            string surveyPath = null;
            bool verbose = false;

            try
            {
                for (int argNum = 0; argNum < args.Length; argNum++)
                {
                    if (args[argNum] == "--a" || args[argNum] == "-attendees")
                    {
                        attendeePath = args[++argNum];
                    }
                    else if (args[argNum] == "--p" || args[argNum] == "-presentations")
                    {
                        presentationsPath = args[++argNum];
                    }
                    else if (args[argNum] == "--s" || args[argNum] == "-survey")
                    {
                        surveyPath = args[++argNum];
                    }
                    else if (args[argNum] == "--v" || args[argNum] == "-verbose")
                    {
                        verbose = true;
                    }
                    else
                    {
                        throw new Exception("Unknown argument " + args[argNum]);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            if (attendeePath == null)
            {
                Console.WriteLine("Missing attendees argument");
            }
            if (presentationsPath == null)
            {
                Console.WriteLine("Missing presentations argument");
            }
            if (surveyPath == null)
            {
                Console.WriteLine("Missing survey argument");
            }
            if ((attendeePath == null) || (presentationsPath == null) || (surveyPath == null))
            {
                return;
            }
            if (verbose)
            {
                Console.WriteLine("Attendees = " + attendeePath);
                Console.WriteLine("Presentations = " + presentationsPath);
                Console.WriteLine("Survey = " + surveyPath);
                Console.WriteLine();
            }
            Dictionary<string, AttendeeInfo> attendees = ReadAttendees(attendeePath);

            Dictionary<string, PresentationInfo> presentations = ReadPresentations(presentationsPath);
            if (verbose)
            {
                Console.WriteLine("Candidate Presentations:");
                foreach (KeyValuePair<string, PresentationInfo> kvp in presentations)
                {
                    Console.WriteLine(kvp.Key + ", " + kvp.Value.title + ": " + kvp.Value.votes + " votes");
                }
                Console.WriteLine();
                Console.WriteLine("Attendees");
                foreach (KeyValuePair<string, AttendeeInfo> kvp in attendees)
                {
                    AttendeeInfo attendee = kvp.Value;
                    Console.Write("Conf " + kvp.Key + "{");
                    foreach (string conflict in attendee.conflicts)
                    {
                        Console.Write(conflict + ",");
                    }
                    Console.WriteLine("}" + attendee.vote);
                }
                Console.WriteLine();
            }

            // Read and tally votes
            try
            {
                uint totalVotes = 0;
                uint conflictVotes = 0;
                uint overriddenVotes = 0;
                uint invalidVotes = 0;

                if (verbose)
                {
                    Console.WriteLine("Survey Votes:");
                }
                using (TextFieldParser parser = new TextFieldParser(surveyPath))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.Delimiters = new string[] { "," };
                    parser.HasFieldsEnclosedInQuotes = true;
                    // Throw away the title row.
                    parser.ReadFields();
                    for (int i = 0; !parser.EndOfData; i++)
                    {
                        string[] details = parser.ReadFields();
                        if (details.Length != 3)
                        {
                            throw new Exception("Unexpected field count in row " + i + " of " + surveyPath);
                        }
                        string author = details[2].Substring(details[2].IndexOf("by ") + 3);
                        PresentationInfo presentation;
                        if (presentations.TryGetValue(author, out presentation))
                        {
                            totalVotes++;
                            string confirmationCode = details[1];
                            if (verbose)
                            {
                                Console.Write("Conf " + confirmationCode + ": " + author);
                            }
                            AttendeeInfo attendee;
                            if (attendees.TryGetValue(confirmationCode, out attendee))
                            {
                                if (attendee.vote != null)
                                {
                                    PresentationInfo oldPresentation;
                                    if (presentations.TryGetValue(attendee.vote, out oldPresentation) && (oldPresentation.votes != 0))
                                    {
                                        overriddenVotes++;
                                        oldPresentation.votes--;
                                        if (verbose) Console.Write(" (override previous)");
                                    }
                                    else
                                    {
                                        throw new Exception("Bad previous vote value at row " + i + " of " + surveyPath);
                                    }
                                }
                                bool foundConflict = false;
                                foreach(string conflict in attendee.conflicts)
                                {
                                    if (conflict == author)
                                    {
                                        if (verbose) Console.Write(" (conflict)");
                                        foundConflict = true;
                                        break;
                                    }
                                }
                                if (foundConflict)
                                {
                                    conflictVotes++;
                                }
                                else
                                {
                                    attendee.vote = author;
                                    presentation.votes++;
                                }
                            }
                            else
                            {
                                if (verbose) Console.Write(" (invalid)");
                                invalidVotes++;
                            }
                            if (verbose) Console.WriteLine();
                        }
                        else
                        {
                            throw new Exception("Missing presentation in row " + i + " of " + surveyPath);
                        }
                    }
                }
                if (verbose) Console.WriteLine();

                uint maxVotes = 0;
                foreach (KeyValuePair<string, PresentationInfo> kvp in presentations)
                {
                    maxVotes = Math.Max(maxVotes, kvp.Value.votes);
                    Console.WriteLine(kvp.Value.votes + " Votes: " + kvp.Key + ", " + kvp.Value.title);
                }
                Console.WriteLine("Total Votes = " + totalVotes + ", Overridden Votes = " + overriddenVotes + 
                    ", Conflict Votes = " + conflictVotes + ", Invalid Votes = " + invalidVotes);
                Console.WriteLine();
                Console.WriteLine("Winning Presentations with " + maxVotes + " votes:");
                foreach (KeyValuePair<string, PresentationInfo> kvp in presentations)
                {
                    if (kvp.Value.votes == maxVotes)
                    {
                        Console.WriteLine(kvp.Key + ", " + kvp.Value.title);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
