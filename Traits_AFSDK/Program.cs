//#define COMPLEX
//#define NEWVALS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OSIsoft.AF;
using OSIsoft.AF.Asset;
using OSIsoft.AF.Analysis;
using OSIsoft.AF.Search;
using OSIsoft.AF.EventFrame;

using OSIsoft.AF.PI;
using System.Text.RegularExpressions;

namespace Traits_AFSDK
{
	class Program
	{
		static void Main(string[] args)
		{
			PISystem AF = new PISystems().DefaultPISystem;
			AFDatabase db = AF.Databases["Traits"];
			Console.WriteLine("Working...");

			#if COMPLEX
				//Create/Configure traits if they don't already exist
				//NOTE: Templates for Event Frames, Elements, and Forecast PI Points should already be created
				//		Element Templates need:
				//			Temperature and Humidity Attributes
				//			Analysis with EF Template linked and Trigger Conditions specified
				//		Event Frames/Forecast Points just need to exist
				CreateTraits(db);
				CreateEFTraits(db);
				ConnectAnalysis(db);
				Console.WriteLine("Trait Creation Complete");

				//Find event frames and compare the start values to trait values
				FindEventFrames(db);
			#else				
				GenerateRandomValues(db);
				Console.WriteLine("Value Generation Complete");
				SimpleTraitCreation(db);
				Console.WriteLine("Trait Creation Complete");
				//CheckForValuesOutOfRange(db);
				CheckForValuesOutOfRangeForTemplate(db);
			#endif

			Console.WriteLine("Completed. Press Enter to Exit");
			Console.ReadLine();

			db.CheckIn();
		}

		#region COMPLEX EXAMPLE
		public static void FindEventFrames(AFDatabase db){
			AFSearchToken token = new AFSearchToken(AFSearchFilter.Template, AFSearchOperator.Equal, "VeryHot");
			AFEventFrameSearch search = new AFEventFrameSearch(db, "Find VeryHot", new []{token});
			List<AFEventFrame> efs = new List<AFEventFrame>(search.FindEventFrames());

			foreach(AFEventFrame ef in efs) {
				Console.WriteLine("\n" + ef.StartTime);
				
				//extract the attribute names by doing a regex to find anything contained within single quotes
				MatchCollection matches = Regex.Matches(ef.Attributes[AFAttributeTrait.AnalysisStartTriggerExpression.Abbreviation].GetValue().ToString(), "'[^']*'");

				//were any attributes name found in the TriggerExpression?
				if(matches.Count == 0) {
					Console.WriteLine("No tag caused this event");
				}
				else {
					Console.WriteLine("Tag(s) that caused this event");
					foreach (Match m in matches){
						//find the attribute for this match in the primary referenced element -- have to trim the start and end single quotes before lookup
						AFAttribute attribute = ef.PrimaryReferencedElement.Attributes[m.ToString().Trim('\'').TrimEnd('\'')];
						if(attribute.Trait == null) {
							Console.WriteLine("{0} : {1}", attribute.Name, attribute.GetValue(ef.StartTime));
							//get all the traits this attribute has and display them
							foreach(AFAttribute trait in attribute.GetAttributesByTrait(AFAttributeTrait.AllTraits)){
								Console.WriteLine("\t{0} : {1}", trait.Name, trait.GetValue(ef.StartTime));
							}
						}
					}
				}
            }
		}

		public static void ConnectAnalysis(AFDatabase db) {
			AFElementTemplate temperatureTemplate = db.ElementTemplates["Temperature"];
			AFAnalysisTemplate analysisTemplate = temperatureTemplate.AnalysisTemplates[0];

			//if the analysis is not already configured to write to traits, add configuration to the configString
			string configureAnalysisForTraits = ";SAVETRIGGEREXPRESSION=True;SAVETRIGGERNAME=True";
            if (!analysisTemplate.AnalysisRule.ConfigString.Contains(configureAnalysisForTraits)) {
				analysisTemplate.AnalysisRule.ConfigString += configureAnalysisForTraits;
			}
		}

		public static void CreateEFTraits(AFDatabase db){
			AFElementTemplate EFTemplate = db.ElementTemplates["VeryHot"];
			CreateTraitAttribute(EFTemplate, AFAttributeTrait.AnalysisStartTriggerExpression);
			CreateTraitAttribute(EFTemplate, AFAttributeTrait.AnalysisStartTriggerName);
		}

		public static void CreateTraits(AFDatabase db) {	
			AFElementTemplate temperatureTemplate = db.ElementTemplates["Temperature"];

			AFAttributeTemplate humidityAttribute = temperatureTemplate.AttributeTemplates["Humidity"];
			AFAttributeTemplate tempereratureAttribute = temperatureTemplate.AttributeTemplates["Temperature"];

			//Create traits for humidity and temperature attributes
			CreateTraitAttribute(humidityAttribute, AFAttributeTrait.Forecast, configString: @"\\pmartin-ps1\sinusoid_plus_1d");
			CreateTraitAttribute(humidityAttribute, AFAttributeTrait.LimitHi, defaultValue: 80);
			CreateTraitAttribute(humidityAttribute, AFAttributeTrait.LimitHiHi, defaultValue: 90);
			CreateTraitAttribute(humidityAttribute, AFAttributeTrait.LimitTarget, defaultValue: 50);

			CreateTraitAttribute(tempereratureAttribute, AFAttributeTrait.Forecast, configString: @"\\pmartin-ps1\cdt_plus_1d");
			CreateTraitAttribute(tempereratureAttribute, AFAttributeTrait.LimitHi, defaultValue: 90);
			CreateTraitAttribute(tempereratureAttribute, AFAttributeTrait.LimitHiHi, defaultValue: 100);
			CreateTraitAttribute(tempereratureAttribute, AFAttributeTrait.LimitTarget, defaultValue: 50);
		}

		public static void CreateTraitAttribute(AFObject parent, AFAttributeTrait trait, double defaultValue = 0, string configString = "") {
			//is this trait an anlaysis trait or an attribute trait
			if (AFAttributeTrait.AllAnalyses.Contains(trait)){
				//analysis trait
				AFElementTemplate par = parent as AFElementTemplate;

				//does this trait already exist?
				if (par.AttributeTemplates[trait.Abbreviation] == null)
				{
					AFAttributeTemplate attribute = par.AttributeTemplates.Add(trait.Abbreviation);
					attribute.Trait = trait;

					attribute.Type = typeof(string);
					attribute.IsConfigurationItem = true;
				}
			}		
			else
			{
				//attribute trait
				AFAttributeTemplate par = parent as AFAttributeTemplate;

				//does this trait already exist?
				if(par.AttributeTemplates[trait.Abbreviation] == null) {
					AFAttributeTemplate attribute = par.AttributeTemplates.Add(trait.Abbreviation);
					attribute.Trait = trait;

					//is this a forecast trait or a limit trait
					if (configString.Length > 0) {
						// link to a pre-created PI tag
						attribute.DataReferencePlugIn = AFDataReference.GetPIPointDataReference();
						attribute.ConfigString = configString;
					}
					else{
						//static value that needs to be set
						//NOTE: use of Default UOM not required
						attribute.SetValue(defaultValue, par.DefaultUOM);
					}
				}
			}
		}
		#endregion full

		#region SIMPLE EXAMPLE
		public static void SimpleTraitCreation(AFDatabase db) {
			//Find baseline template
			AFElementTemplate temperatureTemplate = db.ElementTemplates["Temperature_Simple"];

			//Add a Hi/Lo limit to this template
			AFAttributeTemplate temperature = temperatureTemplate.AttributeTemplates["Temperature"];
		
			//If Hi Trait doesn't exist, create it
			if(temperature.AttributeTemplates[AFAttributeTrait.LimitHi.Abbreviation] == null) {
				//create new attribute
				AFAttributeTemplate limitHi = temperature.AttributeTemplates.Add(AFAttributeTrait.LimitHi.Abbreviation); 
				//specify which trait this attribute is supposed to be
				limitHi.Trait = AFAttributeTrait.LimitHi;
				//set the value for the Hi Limit
				limitHi.SetValue(100, limitHi.DefaultUOM);
			}

			//If Lo Trait doesn't exist, create it
			if (temperature.AttributeTemplates[AFAttributeTrait.LimitHi.Abbreviation] == null) {
				AFAttributeTemplate limitLo = temperature.AttributeTemplates.Add(AFAttributeTrait.LimitLo.Abbreviation);
				limitLo.Trait = AFAttributeTrait.LimitLo;
				limitLo.SetValue(0, limitLo.DefaultUOM);
			}
		}

		public static void CheckForValuesOutOfRange(AFDatabase db) {
			//find all attributes that match the name we specified for our Lo attribute (AFAttributeTrait.LimitLo.Abbreviation)
			AFAttributeList los = AFAttribute.FindElementAttributes(db, null, null, null, null, AFElementType.Any, AFAttributeTrait.LimitLo.Abbreviation, null, TypeCode.Double, true, AFSortField.Name, AFSortOrder.Descending, 101);
			//bulk load the Lo values
			AFValues lovals = los.GetValue();
			for (int i = 0; i < los.Count; i++) {
				//if the lo value is greater than the parent (attribute) value, output to console
				if(lovals[i].ValueAsDouble() > los[i].Parent.GetValue().ValueAsDouble()) {
					Console.WriteLine("Value under Lo Limit for {0}  :  {1} < {2}", los[i].Element.Name, los[i].Parent.GetValue().ValueAsDouble(), lovals[i].ValueAsDouble());
				}
			}

			//repeat for hi vals
			AFAttributeList his = AFAttribute.FindElementAttributes(db, null, null, null, null, AFElementType.Any, AFAttributeTrait.LimitHi.Abbreviation, null, TypeCode.Double, true, AFSortField.Name, AFSortOrder.Descending, 101);
			AFValues hivals = his.GetValue();
			for (int i = 0; i < his.Count; i++) {
				if (hivals[i].ValueAsDouble() < his[i].Parent.GetValue().ValueAsDouble()) {
					Console.WriteLine("Value over Hi Limit for {0}  :  {1} > {2}", his[i].Element.Name, his[i].Parent.GetValue().ValueAsDouble(), hivals[i].ValueAsDouble());
				}
			}
		}

		public static void CheckForValuesOutOfRangeForTemplate(AFDatabase db) {
			//Get Temperature Template
			AFElementTemplate temperatureTemplate = db.ElementTemplates["Temperature_Simple"];
			//Find all attributes that belong to an element of this template
			AFAttributeList al = AFAttribute.FindElementAttributes(db, null, null, null, temperatureTemplate, AFElementType.Any, "Temperature", null, TypeCode.Double, true, AFSortField.Name, AFSortOrder.Ascending, 200);
			//bulk load values
			AFValues alval = al.GetValue();
			
			foreach(AFValue v in alval) {
				//get the trait values
				AFAttribute lo_a = v.Attribute.GetAttributeByTrait(AFAttributeTrait.LimitLo);
				AFAttribute hi_a = v.Attribute.GetAttributeByTrait(AFAttributeTrait.LimitHi);

				//compare to trait values if the trait exists
				if(hi_a != null && v.ValueAsDouble() > hi_a.GetValue().ValueAsDouble()) {
					Console.WriteLine("Value over Hi Limit for {0}  :  {1} > {2}", v.Attribute.Element.Name, v.ValueAsDouble(), hi_a.GetValue().ValueAsDouble());
				}
				else if(lo_a != null && v.ValueAsDouble() < lo_a.GetValue().ValueAsDouble()) {
					Console.WriteLine("Value under Lo Limit for {0}  :  {1} < {2}", v.Attribute.Element.Name, v.ValueAsDouble(), lo_a.GetValue().ValueAsDouble());
				} 
			}
		}

		public static void GenerateRandomValues(AFDatabase db) {
			AFElementTemplate temperatureTemplate = db.ElementTemplates["Temperature_Simple"];
			Random r = new Random();

			//do we have the 100 elements already created?
			if(db.Elements.Count < 100) {
				//no, create 100 elements
				for (int i = 0; i < 100; i++) {
					AFElement e = db.Elements.Add("Location"+ i);
					e.Template = temperatureTemplate;
					e.Attributes["Humidity"].SetValue(new AFValue(r.Next(0,100)));
					e.Attributes["Temperature"].SetValue(new AFValue(r.Next(-20, 120)));
				}
				db.CheckIn();
			}
			else
			{
				//yes, we have 100 elements
				//do we want to generate new ones?  If so, uncomment #define NEWVALS (on line 2). I would strongly advise against generating new values every run.  Use this option sparingly.
				#if NEWVALS
				AFAttributeList al = AFAttribute.FindElementAttributes(db, null, null, null, temperatureTemplate, AFElementType.Any, "Temperature", null, TypeCode.Double, true, AFSortField.Name, AFSortOrder.Ascending, 200);
				foreach(AFAttribute a in al) {
					a.SetValue(new AFValue(r.Next(-20, 120)));
				}

				al = AFAttribute.FindElementAttributes(db, null, null, null, temperatureTemplate, AFElementType.Any, "Humidity", null, TypeCode.Double, true, AFSortField.Name, AFSortOrder.Ascending, 200);
				foreach (AFAttribute a in al)
				{
					a.SetValue(new AFValue(r.Next(0, 100)));
				}
				#endif
			}
		}

		public static void DeleteElementsFromTraits(AFDatabase db) {
			for(int i = db.Elements.Count - 1; i >= 0; i--) {
				db.Elements[i].Delete();
			}
			db.CheckIn();
		}
#endregion simple
	}
}
