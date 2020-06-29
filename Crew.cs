using System;
using System.Collections.Generic;


/// <summary>
/// Class to contain crew details. Set all to string b/c of the inconsistency in data.
/// </summary>
public class Crew
{
    public string OriginalUrl { get; set; }
    public string Name { get; set; }
    public string PictureUrl { get; set; }
    public string DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; }
    public string ServiceNumber { get; set; }
    public string Rank { get; set; }
    public string DateOfEnlistment { get; set; }
    public string PlaceOfEnlistment { get; set; }
    public string DateOnShip { get; set; }
    public string DateOffShip { get; set; }
    public string DateDischarged { get; set; }
    public string DateOfDeath { get; set; }
    public string Spouse { get; set; }
    public List<string> HighSchool { get; set; }
    public List<string> Children { get; set; }
    public List<string> College { get; set; }
    public List<string> Grandchildren { get; set; }
    public List<string> Interests { get; set; }
    public List<string> GreatGrandchildren { get; set; }
    public string PostWarExperience { get; set; }
}