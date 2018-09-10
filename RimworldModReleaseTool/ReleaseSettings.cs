using System;
using System.Xml.Serialization;
using System.Collections.Generic;
namespace RimworldModReleaseTool
{
    [System.Xml.Serialization.XmlRoot("ReleaseSettings")]
    public class ReleaseSettings {
        [XmlElement(ElementName="handleGitHub")]
        public bool HandleGitHub { get; set; }
        [XmlElement(ElementName="handleLudeon")]
        public bool HandleLudeon { get; set; }
        [XmlElement(ElementName="handleSteam")]
        public bool HandleSteam { get; set; }
        [XmlElement(ElementName="handleDiscord")]
        public bool HandleDiscord { get; set; }
        [XmlElement(ElementName="handleDiscordWebhook")]
        public bool HandleDiscordWebhook { get; set; }
        [XmlElement(ElementName="handlePatreon")]
        public bool HandlePatreon { get; set; }
        [XmlElement(ElementName="showCopyableNotes")]
        public bool ShowCopyableNotes { get; set; }
        [XmlElement(ElementName="copyableNotesProgram")]
        public string CopyableNotesProgram { get; set; }
        [XmlElement(ElementName="filteredWhenCopied")]
        public string FilteredWhenCopied { get; set; }
        [XmlElement(ElementName="filteredWhenZipped")]
        public string FilteredWhenZipped { get; set; }
        [XmlElement(ElementName="githubProductHeaderValue")]
        public string GithubProductHeaderValue { get; set; }
        [XmlElement(ElementName="githubCommitter")]
        public string GithubCommitter { get; set; }
        [XmlElement(ElementName="githubCommitterEmail")]
        public string GithubCommitterEmail { get; set; }
    }
}
