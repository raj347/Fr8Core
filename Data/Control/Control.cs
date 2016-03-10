﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Data.Constants;
using Data.Crates;
using Data.Helpers;
using Data.Interfaces.DataTransferObjects;
using Data.Interfaces.Manifests;
using Newtonsoft.Json;

namespace Data.Control
{
    /// <summary>
    /// This interface is applied to controls and control data items (e.g. radio buttons)
    /// that support nested controls.
    /// </summary>
    public interface ISupportsNestedFields
    {
        IList<ControlDefinitionDTO> Controls { get; }
    }

    public interface IResettable
    {
        void Reset(List<string> fieldNames = null);
    }

    public class ControlTypes
    {
        public const string TextBox = "TextBox";
        public const string CheckBox = "CheckBox";
        public const string DropDownList = "DropDownList";
        public const string RadioButtonGroup = "RadioButtonGroup";
        public const string FilterPane = "FilterPane";
        public const string MappingPane = "MappingPane";
        public const string TextBlock = "TextBlock";
        public const string FilePicker = "FilePicker";
        public const string Routing = "Routing";
        public const string FieldList = "FieldList";
        public const string Button = "Button";
        public const string TextSource = "TextSource";
        public const string TextArea = "TextArea";
        public const string QueryBuilder = "QueryBuilder";
        public const string ManageRoute = "ManageRoute";
        public const string Duration = "Duration";
        public const string RunRouteButton = "RunRouteButton";
        public const string UpstreamDataChooser = "UpstreamDataChooser";
        public const string UpstreamFieldChooser = "UpstreamFieldChooser";
        public const string UpstreamCrateChooser = "UpstreamCrateChooser";
        public const string DatePicker = "DatePicker";
        public const string CrateChooser = "CrateChooser";
    }

    public class CheckBox : ControlDefinitionDTO
    {
        public CheckBox()
        {
            Type = ControlTypes.CheckBox;
        }
    }

    public class RunRouteButton : ControlDefinitionDTO
    {
        public RunRouteButton()
        {
            Type = ControlTypes.RunRouteButton;
        }
    }

    public class DropDownList : ControlDefinitionDTO
    {
        [JsonProperty("listItems")]
        public List<ListItem> ListItems { get; set; }

        [JsonProperty("selectedKey")]
        public string selectedKey { get; set; }

        public DropDownList() : base()
        {
            ListItems = new List<ListItem>();
            Type = "DropDownList";
        }
    }

    public class RadioButtonGroup : ControlDefinitionDTO
    {
        [JsonProperty("groupName")]
        public string GroupName { get; set; }

        [JsonProperty("radios")]
        public List<RadioButtonOption> Radios { get; set; }

        public RadioButtonGroup()
        {
            Radios = new List<RadioButtonOption>();
            Type = ControlTypes.RadioButtonGroup;
        }
    }

    public class TextBox : ControlDefinitionDTO
    {
        public TextBox()
        {
            Type = ControlTypes.TextBox;
        }
    }

    public class DatePicker : ControlDefinitionDTO
    {
        public DatePicker()
        {
            Type = ControlTypes.DatePicker;
        }
    }

    public class QueryBuilder : ControlDefinitionDTO
    {
        public QueryBuilder()
        {
            Type = ControlTypes.QueryBuilder;
        }
    }

    public class FilterPane : ControlDefinitionDTO
    {
        [JsonProperty("fields")]
        public List<FilterPaneField> Fields { get; set; }

        public FilterPane()
        {
            Type = ControlTypes.FilterPane;
        }
    }

    public class MappingPane : ControlDefinitionDTO
    {
        public MappingPane()
        {
            Type = ControlTypes.MappingPane;
        }
    }

    public class Generic : ControlDefinitionDTO
    {
        public Generic()
        {
            Type = ControlTypes.TextBox; // Yes, default to TextBox
        }
    }

    public class TextArea : ControlDefinitionDTO
    {
        [JsonProperty("isReadOnly")]
        public bool IsReadOnly { get; set; }

        public TextArea() :
            base(ControlTypes.TextArea)
        {
        }
    }

    public class TextBlock : ControlDefinitionDTO
    {
        [JsonProperty("class")]
        public string CssClass
        {
            get;
            set;
        }

        public TextBlock()
        {
            Type = ControlTypes.TextBlock;
        }
    }

    public class FilePicker : ControlDefinitionDTO
    {
        public FilePicker()
        {
            Type = ControlTypes.FilePicker;
        }
    }

    public class FieldList : ControlDefinitionDTO
    {
        public FieldList()
        {
            Type = ControlTypes.FieldList;
        }

        public override void Reset(List<string> fieldNames)
        {
            if (fieldNames != null)
            {
                //key-value pairs are immutable, we need to crate a new List
                var newList = new List<KeyValuePair<string, string>>();
                var keyValuePairs = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(Value);
                foreach (var keyValuePair in keyValuePairs)
                {
                    if (fieldNames.Any(n => n == keyValuePair.Key))
                    {
                        newList.Add(new KeyValuePair<string, string>(keyValuePair.Key, ""));
                    }
                    else
                    {
                        newList.Add(keyValuePair);
                    }
                }
                Value = JsonConvert.SerializeObject(newList);
            }
            else
            {
                Value = "[]";
            }
        }
    }

    public class TextSource : DropDownList
    {
        [JsonProperty("initialLabel")]
        public string InitialLabel;

        [JsonProperty("upstreamSourceLabel")]
        public string UpstreamSourceLabel;

        [JsonProperty("textValue")]
        public string TextValue;

        [JsonProperty("valueSource")]
        public string ValueSource;

        public TextSource() : base()
        {
            Type = ControlTypes.TextSource;
        }

        public TextSource(string initialLabel, string upstreamSourceLabel, string name) : base()
        {
            Type = ControlTypes.TextSource;
            this.InitialLabel = initialLabel;
            this.Name = name;
            Source = new FieldSourceDTO
            {
                Label = upstreamSourceLabel,
                ManifestType = CrateManifestTypes.StandardDesignTimeFields
            };
        }

        public string GetValue(ICrateStorage payloadCrateStorage, bool ignoreCase = false, MT? manifestType = null, string label = null)
        {
            switch (ValueSource)
            {
                case "specific":
                    return TextValue;

                case "upstream":
                    return GetPayloadValue(payloadCrateStorage, ignoreCase, manifestType, label);

                default:
                    return null;
            }
        }

        public string GetPayloadValue(ICrateStorage payloadStorage, bool ignoreCase = false, MT? manifestType = null, string label = null)
        {
            //search through every crate except operational state crate
            Expression<Func<Crate, bool>> defaultSearchArguments = (c) => c.ManifestType.Id != (int)MT.OperationalStatus;

            //apply label criteria if not null
            if (label != null)
            {
                Expression<Func<Crate, bool>> andLabel = (c) => c.Label == label;
                defaultSearchArguments = Expression.Lambda<Func<Crate, bool>>(Expression.AndAlso(defaultSearchArguments, andLabel), defaultSearchArguments.Parameters);
            }

            //apply manifest criteria if not null
            if (manifestType != null)
            {
                Expression<Func<Crate, bool>> andManifestType = (c) => c.ManifestType.Id == (int)manifestType;
                defaultSearchArguments = Expression.Lambda<Func<Crate, bool>>(Expression.AndAlso(defaultSearchArguments, andManifestType), defaultSearchArguments.Parameters);
            }

            //find user requested crate
            var foundCrates = payloadStorage.Where(defaultSearchArguments.Compile()).ToList();
            if (!foundCrates.Any())
            {
                return null;
            }

            //get operational state crate to check for loops
            var operationalState = payloadStorage.CrateContentsOfType<OperationalStateCM>().Single();
            //iterate through found crates to find the payload
            foreach (var foundCrate in foundCrates)
            {
                var foundField = FindField(operationalState, foundCrate);
                if (foundField != null)
                {
                    return foundField.Value;
                }
            }

            return null;
        }


        private FieldDTO FindField(OperationalStateCM operationalState, Crate crate)
        {
            object searchArea = null;
            //let's check if we are in a loop
            //and this is a loop data?
            //check if this crate is loop related
            var deepestLoop = operationalState.Loops.OrderByDescending(l => l.Level).FirstOrDefault(l => !l.BreakSignalReceived && l.Label == crate.Label && l.CrateManifest == crate.ManifestType.Type);
            if (deepestLoop != null) //this is a loop related data request
            {
                //find current element
                var dataList = Fr8ReflectionHelper.FindFirstArray(crate.Get());
                //we will search requested field in current element
                searchArea = dataList[deepestLoop.Index];
            }
            else
            {
                //hmmm this is a regular data request
                //lets search in complete crate
                searchArea = crate;
            }

            //we should find first related field and return
            var fields = Fr8ReflectionHelper.FindFieldsRecursive(searchArea);
            var fieldMatch = fields.FirstOrDefault(f => f.Key == this.selectedKey);
            //let's return first match
            return fieldMatch;
        }

        /// <summary>
        /// Extracts crate with specified label and ManifestType = Standard Design Time,
        /// then extracts field with specified fieldKey.
        /// </summary>
        private string ExtractPayloadFieldValue(ICrateStorage payloadCrateStorage, bool ignoreCase)
        {
            var fieldValues = payloadCrateStorage.CratesOfType<StandardPayloadDataCM>().SelectMany(x => x.Content.GetValues(selectedKey, ignoreCase))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            if (fieldValues.Length > 0)
                return fieldValues[0];

            throw new ApplicationException(string.Format("No field found with specified key: {0}.", selectedKey));
        }
    }

    public class Button : ControlDefinitionDTO
    {
        [JsonProperty("class")]
        public string CssClass;

        /// <summary>
        /// Where the button was clicked before the current /configure request was sent.
        /// Used to recognize 'click' event on server-side.
        /// </summary>
        [JsonProperty("clicked")]
        public bool Clicked;

        public Button()
        {
            Type = ControlTypes.Button;
        }
    }

    public class Duration : ControlDefinitionDTO
    {
        public Duration()
        {
            Type = ControlTypes.Duration;
            InnerLabel = "Choose duration:";
        }

        [JsonProperty("value")]
        public new TimeSpan Value
        {
            get
            {
                return new TimeSpan(this.Days, this.Hours, this.Minutes, 0);
            }
        }

        [JsonProperty("innerLabel")]
        public string InnerLabel { get; set; }

        [JsonProperty("days")]
        public Int32 Days { get; set; }

        [JsonProperty("hours")]
        public Int32 Hours { get; set; }

        [JsonProperty("minutes")]
        public Int32 Minutes { get; set; }

    }




    public class FieldSourceDTO
    {
        [JsonProperty("manifestType")]
        public string ManifestType { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("filterByTag")]
        public string FilterByTag { get; set; }

        [JsonProperty("requestUpstream")]
        public bool RequestUpstream { get; set; }

        public FieldSourceDTO()
        {
        }

        public FieldSourceDTO(string manifestType, string label)
        {
            ManifestType = manifestType;
            Label = label;
        }
    }

    public class ControlEvent
    {
        public static ControlEvent RequestConfig
        {
            get
            {
                return new ControlEvent("onChange", "requestConfig");
            }
        }

        public static ControlEvent RequestConfigOnClick
        {
            get
            {
                return new ControlEvent("onClick", "requestConfig");
            }
        }

        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("handler")]
        public string Handler { get; set; }

        public ControlEvent(string name, string handler)
        {
            Name = name;
            Handler = handler;
        }

        public ControlEvent()
        {
        }
    }

    public class RadioButtonOption : ISupportsNestedFields
    {
        public RadioButtonOption()
        {
            Controls = new List<ControlDefinitionDTO>();
        }

        [JsonProperty("selected")]
        public bool Selected { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("controls")]
        public IList<ControlDefinitionDTO> Controls { get; set; }
    }

    public class FilterPaneField
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ListItem
    {
        [JsonProperty("selected")]
        public bool Selected { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    public class UpstreamDataChooser : ControlDefinitionDTO
    {
        public UpstreamDataChooser()
        {
            Type = ControlTypes.UpstreamDataChooser;
        }

        [JsonProperty("selectedManifest")]
        public string SelectedManifest { get; set; }

        [JsonProperty("selectedLabel")]
        public string SelectedLabel { get; set; }

        [JsonProperty("selectedFieldType")]
        public string SelectedFieldType { get; set; }
    }

    public class CrateDetails
    {
        [JsonProperty("manifestType")]
        public DropDownList ManifestType { get; set; }

        [JsonProperty("label")]
        public DropDownList Label { get; set; }
    }

    public class UpstreamCrateChooser : ControlDefinitionDTO
    {
        public UpstreamCrateChooser()
        {
            Type = ControlTypes.UpstreamCrateChooser;
        }

        [JsonProperty("selectedCrates")]
        public List<CrateDetails> SelectedCrates { get; set; }

        [JsonProperty("multiSelection")]
        public bool MultiSelection { get; set; }

    }

    public class CrateChooser : ControlDefinitionDTO
    {
        public CrateChooser()
        {
            Type = ControlTypes.CrateChooser;
        }

        [JsonProperty("crateDescriptions")]
        public List<CrateDescriptionDTO> CrateDescriptions { get; set; }

        [JsonProperty("singleManifestOnly")]
        public bool SingleManifestOnly { get; set; }

        [JsonProperty("requestUpstream")]
        public bool RequestUpstream { get; set; }
    }

    public class UpstreamFieldChooser : ControlDefinitionDTO
    {
        public UpstreamFieldChooser()
        {
            Type = ControlTypes.UpstreamFieldChooser;
        }
    }

    public class DocumentationDTO
    {
        public DocumentationDTO(string displayMechanism, string contentPath)
        {
            this.DisplayMechanism = displayMechanism;
            this.ContentPath = contentPath;
        }

        [JsonProperty("displayMechanism")]
        public string DisplayMechanism { get; set; }

        [JsonProperty("contentPath")]
        public string ContentPath { get; set; }

        [JsonProperty("url")]
        public string URL
        {
            get
            {
                if (string.IsNullOrEmpty(ContentPath))
                    return "/activites/documentation";
                return string.Format("/activites/documentation/{0}", ContentPath);
            }
        }
    }
}
