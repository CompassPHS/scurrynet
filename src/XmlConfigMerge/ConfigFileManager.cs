using System;
using System.Xml;
using System.IO;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace Tools.XmlConfigMerge
{
    // Adapted from: http://www.codeproject.com/Articles/7406/XmlConfigMerge-Merge-config-file-settings

	/// <summary>
	/// Manages config file reading and writing, and optional merging of two config files.
	/// </summary>
	[Serializable]
	public class ConfigFileManager
	{
		public ConfigFileManager(string masterConfigPath)
            : this(masterConfigPath, null)
        {
		
        }

		public ConfigFileManager(string masterConfigPath, string mergeFromConfigPath)
				: this(masterConfigPath, mergeFromConfigPath, false) 
        {
            // makeMergeFromConfigPathTheSavePath
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="masterConfigPath"></param>
		/// <param name="mergeFromConfigPath"></param>
		/// <param name="makeMergeFromConfigPathTheSavePath"></param>
		/// <exception cref="Exception">if mergeFromConfigPath is specified but does not exist, and makeMergeFromConfigPathTheSavePath is false</exception>
		public ConfigFileManager(string masterConfigPath, string mergeFromConfigPath, bool makeMergeFromConfigPathTheSavePath)
        {
			_masterConfigPath = masterConfigPath;
			_configPath = masterConfigPath;
			_fromLastInstallConfigPath = mergeFromConfigPath;
			
			if (mergeFromConfigPath != null && ( ! File.Exists(mergeFromConfigPath) ) && ! makeMergeFromConfigPathTheSavePath)
            {
				throw new ApplicationException("Specified mergeFromConfigPath does not exist: " + mergeFromConfigPath);
			}
			
			try
            {				
				using (FileStream rd = new FileStream(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite) )
				{
					_xmlDocument.Load(rd);
				}
			}
            catch (Exception ex)
            {
				throw new ApplicationException("Could not open '" + ConfigPath + "': " + ex.Message);
			}		
			
			if (mergeFromConfigPath != null
					&& File.Exists(mergeFromConfigPath) )
            {
				Debug.WriteLine("Merging from " + mergeFromConfigPath);					
				//Merge approach:
				//	x Use from-last-install config as base
				//	x Merge in any non-existing keyvalue pairs from distrib-config
				//	x Use this merged config, and leave the last-install-config file in place for reference
				//Merge, preserving any comments from distrib-one only
				
				XmlDocument reInstallConfig = new XmlDocument();
				try
                {
					reInstallConfig.Load(mergeFromConfigPath);
				}
                catch (Exception ex)
                {
					_fromLastInstallConfigPath = null;
					throw new ApplicationException("Could not read existing config '" 
							+ mergeFromConfigPath 
							+ "': " 
							+ ex.Message, 
							ex);
				}					
				
				UpdateExistingElementsAndAttribs(reInstallConfig, _xmlDocument);
			}
			
			if (makeMergeFromConfigPathTheSavePath)
            {
				_configPath = _fromLastInstallConfigPath;
			}
		}
		
		private string _configPath;
		public string ConfigPath
        {
			get 
            {
                return _configPath;
            }
		}

		private string _masterConfigPath;
		public string MasterConfigPath
        {
			get
            {
                return _masterConfigPath;
            }
		}

		private string _fromLastInstallConfigPath = null;
		public string FromLastInstallConfigPath
        {
			get
            {
                return _fromLastInstallConfigPath;
            }
		}
		
		public bool FromLastInstallConfigPathExists()
        {
			if (FromLastInstallConfigPath == null)
            {
				return false;
			}
			
			return File.Exists(FromLastInstallConfigPath);
		}
		
		public string GetXPathValue(string xPath)
        {
			XmlNode node = XmlDocument.SelectSingleNode(xPath);
			if (node == null)
            {
				return null;
			}
			
			return node.InnerText;
		}
		
		public string[] ReplaceXPathValues(string xPath, string replaceWith)
        {
			return ReplaceXPathValues(xPath, replaceWith, null);
		}

		/// <summary>
		/// Search and replace on one or more specified values as specified by a single xpath expressino
		/// </summary>
		/// <param name="xPath"></param>
		/// <param name="replaceWith"></param>
		/// <param name="regexPattern">Optionally specify a regex pattern to search within the found values. 
		///	If a single () group is found within this expression, only this portion is replaced.</param>
		/// <returns></returns>
		/// <exception cref="ApplicationException">When no nodes match xpath-expression and no regexPattern is specified (is null), 
		/// and can't auto-create the node (is not an appSettings expression).</exception>
		public string[] ReplaceXPathValues(string xPath, string replaceWith, Regex regexPattern)
        {
			if (xPath == null || xPath == string.Empty)
            {
				throw new ApplicationException("Required xPath is blank or null");
			}
			
            if (replaceWith == null)
            {
				throw new ApplicationException("Required replaceWith is null");
			}
			
            ArrayList ret = new ArrayList();
			XmlNodeList nodes = _xmlDocument.SelectNodes(xPath);
			
			//Check if no existing nodes match
			if (nodes.Count == 0)
            {
				if (regexPattern != null)
                {
					return (string[] ) ret.ToArray(typeof(string) ); //no error or auto-create attempt when a pattern was specified
				}
				
				//Check special case of fully-qualified appSetting key/value pair
				Regex regex = new Regex(@"/configuration/appSettings/add\[\@key=['""](.*)['""]\]/\@value");
				Match match = regex.Match(xPath);
				
                if ( ! match.Success)
                {
					throw new ApplicationException("'" 
							+ xPath 
							+ "' does not match any nodes, and may not be auto-created since it is not of form '" 
							+ regex.ToString() + "'"); //is an error when no pattern specified and can't auto-create
				}
				
				//Auto create this one
				string key = match.Result("$1");
				SetKeyValue(key, ""); //Value is set below (unless no regex match)
				ret.Add("add/@key" + " created for key '" + key + "'");
				
				nodes = _xmlDocument.SelectNodes(xPath);
			}
			
			//Proceed with replacements
			bool replacedOneOrMore = false;
			
            foreach (XmlNode node in nodes)
            {
				string replText = null;
				
                if (regexPattern != null)
                {
					Match match = regexPattern.Match(node.InnerText);
					
                    if (match.Success)
                    {
						//Determine if group match applies
						switch (match.Groups.Count)
                        {
							case 1:
								replText = regexPattern.Replace(node.InnerText, replaceWith);
								break;
							case 2:
								replText = string.Empty;
								
                                if (match.Groups[2 - 1].Index > 0)
                                {
									replText += node.InnerText.Substring(0, match.Groups[2 - 1].Index);
								}
								
                                replText += replaceWith;
								int firstPostGroupPos = match.Groups[2 - 1].Index + match.Groups[2 - 1].Length;
								
                                if (node.InnerText.Length > firstPostGroupPos)
                                {
									replText += node.InnerText.Substring(firstPostGroupPos);
								}

								break;
							default:
								throw new ApplicationException("> 1 regex replace group not supported (" 
										+ match.Groups.Count + ") for regex expr: '" 
										+ regexPattern.ToString() + "'");
						}
					}
				}
                else
                {
					replText = replaceWith;
				}
				
				if (replText != null)
                {
					replacedOneOrMore = true;
					node.InnerText = replText;
					if (node is XmlAttribute)
                    {
						XmlAttribute keyAttrib = ( (XmlAttribute) node).OwnerElement.Attributes["key"];
						if (keyAttrib == null)
                        {
							ret.Add(( (XmlAttribute) node).OwnerElement.Name + "/@" + node.Name + " set to '" + replText + "'");							
						}
                        else
                        {
							ret.Add(( (XmlAttribute) node).OwnerElement.Name + "[@key='" + keyAttrib.InnerText + "']/@" + node.Name + " set to '" + replText + "'");
						}
					}
                    else
                    {
						ret.Add(node.Name + " set to '" + replText);
					}
				}
			}
			
			if ( ! replacedOneOrMore)
            {
				ret.Add("No values matched replace pattern '" 
						+ regexPattern.ToString()
						+ "' for '"
						+ xPath + "'");
			}
			
			return (string[] ) ret.ToArray(typeof(string) );
		}
		
		private XmlDocument _xmlDocument = new XmlDocument();
		public XmlDocument XmlDocument
        {
			get
            {
                return _xmlDocument;
            }
		}
		
		public virtual void Save()
        {
			Save(ConfigPath);
		}

		public virtual void Save(string saveAsName)
        {
			//Ensure not set r/o
			if (File.Exists(saveAsName))
            {
				ClearSpecifiedFileAttributes(saveAsName, FileAttributes.ReadOnly);
			}				

			_xmlDocument.Save(saveAsName);
		}
		
		public string GetKeyValue(string key)
        {
			if (key == null || key == string.Empty)
            {
				throw new ApplicationException("Required key is blank or null");
			}
			
            XmlNode node = GetKeyValueNode(key);
			
            if (node == null)
            {
				return null;
			}

			return node.Attributes["value"].InnerText;
		}
		
		private XmlNode GetKeyValueNode(string key) 
		{
			XmlNodeList nodes = XmlDocument.SelectNodes("/configuration/appSettings/add[@key='" + key + "']");
			
            if (nodes.Count == 0)
            {
				return null;
			}
			
			return nodes[nodes.Count - 1]; //Return last (to be compat with System.Configuration behavior)
		}
		
		public Hashtable GetAllKeyValuePairs()
        {
			Hashtable ret = new Hashtable();
			
			foreach (XmlNode node in XmlDocument.SelectNodes("/configuration/appSettings/add"))
            {
				ret.Add(node.Attributes["key"].InnerText, node.Attributes["value"].InnerText);
			}
			
			return ret;
		}
		
		public void SetKeyValue(string key, string value)
        {
			if (key == null || key == string.Empty)
            {
				throw new ApplicationException("Required key is blank or null");
			}
			
            if (value == null)
            {
				throw new ApplicationException("Required value is null");
			}
			
            XmlNode node = GetKeyValueNode(key);
			
            if (node == null)
            {
				XmlNode top = XmlDocument.SelectSingleNode("/configuration");
				
                if (top == null)
                {
					top = XmlDocument.AppendChild(XmlDocument.CreateElement("configuration") );
				}
				
                XmlNode app = top.SelectSingleNode("appSettings");
				
                if (app == null)
                {
					app = top.AppendChild(XmlDocument.CreateElement("appSettings") );
				}
				
                node = app.AppendChild(XmlDocument.CreateElement("add") );
				node.Attributes.Append(XmlDocument.CreateAttribute("key") ).InnerText = key;
				node.Attributes.Append(XmlDocument.CreateAttribute("value") );
			}
			
			node.Attributes["value"].InnerText = value;
		}

		private void ClearSpecifiedFileAttributes(string path, FileAttributes fileAttributes) 
		{
			File.SetAttributes(path, File.GetAttributes(path) & (FileAttributes) ( (FileAttributes) 0x7FFFFFFF - fileAttributes) );
		}		

		/// <summary>
		/// Merge element and attribute values from one xml doc to another.
		/// </summary>
		/// <param name="fromXdoc"></param>
		/// <param name="toXdoc"></param>
		/// <remarks>
		/// Multiple same-named peer elements, are merged in the ordinal order they appear.
		/// </remarks>
		public static void UpdateExistingElementsAndAttribs(XmlDocument fromXdoc, XmlDocument toXdoc) 
		{
			UpdateExistingElementsAndAttribsRecurse(fromXdoc.ChildNodes, toXdoc);
		}
		
		private static void UpdateExistingElementsAndAttribsRecurse(XmlNodeList fromNodes, XmlNode toParentNode)
        {
			int iSameElement = 0;
			XmlNode lastElement = null;
			
            foreach (XmlNode node in fromNodes)
            {
				if (node.NodeType != XmlNodeType.Element)
                {
					continue;
				}

				if (lastElement != null
						&& node.Name == lastElement.Name && node.NamespaceURI == lastElement.NamespaceURI)
                {
					iSameElement++;
				}
                else
                {
					iSameElement = 0;
				}
				
                lastElement = node;
				
				XmlNode toNode;
				
                if (node.Attributes["key"] != null)
                {
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["key"] );
				}
                else if (node.Attributes["name"] != null)
                {
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["name"] );
				}
                else if (node.Attributes["type"] != null)
                {					
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, node.Attributes["type"] );
				}
                else
                {
					toNode = SelectSingleNodeMatchingNamespaceURI(toParentNode, node, iSameElement);
				}
				
				if (toNode == null)
                {
					if (node == null)
					{
						throw new ApplicationException("node == null");
					}
					
                    if (node.Name == null)
					{
						throw new ApplicationException("node.Name == null");
					}
					
                    if (toParentNode == null)
					{
						throw new ApplicationException("toParentNode == null");
					}
					
                    if (toParentNode.OwnerDocument == null)
					{
						throw new ApplicationException("toParentNode.OwnerDocument == null");
					}
									
					Debug.WriteLine("app: " + toParentNode.Name + "/" + node.Name);			
					
                    if (node.ParentNode.Name != toParentNode.Name)
                    {
						throw new ApplicationException("node.ParentNode.Name != toParentNode.Name: " + node.ParentNode.Name + " !=" + toParentNode.Name);
					}
					
                    try
                    {
						toNode = toParentNode.AppendChild(toParentNode.OwnerDocument.CreateElement(node.Name) );
					}
                    catch (Exception ex)
                    {
						throw new ApplicationException("ex during toNode = toParentNode.AppendChild(: " + ex.Message);
					}
				}
				
				//Copy element content if any
				XmlNode textEl = GetTextElement(node);

				if (textEl != null)
                {
					toNode.InnerText = textEl.InnerText;
				}
				
				//Copy attribs if any
				foreach (XmlAttribute attrib in node.Attributes)
                {
					XmlAttribute toAttrib = toNode.Attributes[attrib.Name];
					
                    if (toAttrib == null)
                    {
						Debug.WriteLine("attr: " + toNode.Name + "@" + attrib.Name);
						toAttrib = toNode.Attributes.Append(toNode.OwnerDocument.CreateAttribute(attrib.Name));
					}
					
                    toAttrib.InnerText = attrib.InnerText;
				}
				
                ((XmlElement) toNode).IsEmpty = ! toNode.HasChildNodes; //Ensure no endtag when not needed
				UpdateExistingElementsAndAttribsRecurse(node.ChildNodes, toNode);
			}
		}
		
		private static XmlNode GetTextElement(XmlNode node) 
		{
			foreach (XmlNode subNode in node.ChildNodes) 
			{
				if (subNode.NodeType == XmlNodeType.Text) 
				{
					return subNode;
				}
			}
			
			return null;
		}
		
		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName) 
		{
			return SelectSingleNodeMatchingNamespaceURI(node, nodeName, null);
		}
		
		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, int iSameElement)
		{
			return SelectSingleNodeMatchingNamespaceURI(node, nodeName, null, iSameElement);
		}
		
		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, XmlAttribute keyAttrib) 
		{
			return SelectSingleNodeMatchingNamespaceURI(node, nodeName, keyAttrib, 0);
		}
	
		private static Regex _typeParsePattern = new Regex(@"([^,]+),");
	
		private static XmlNode SelectSingleNodeMatchingNamespaceURI(XmlNode node, XmlNode nodeName, XmlAttribute keyAttrib, int iSameElement)
		{
			XmlNode matchNode = null;
			int iNodeNameElements = 0 - 1;
			
            foreach (XmlNode subNode in node.ChildNodes) 
			{
				if (subNode.Name != nodeName.Name || subNode.NamespaceURI != nodeName.NamespaceURI) 
				{
					continue;
				}
				
				iNodeNameElements++;
				
				if (keyAttrib == null) 
				{
					if (iNodeNameElements == iSameElement) 
					{
						return subNode;
					} 
					else 
					{
						continue;
					}
				}
				
				if (subNode.Attributes[keyAttrib.Name] != null && 
						subNode.Attributes[keyAttrib.Name].InnerText == keyAttrib.InnerText)
                {
					matchNode = subNode;
				}
                else if (keyAttrib != null 
						&& keyAttrib.Name == "type")
                {
					Match subNodeMatch = _typeParsePattern.Match(subNode.Attributes[keyAttrib.Name].InnerText);
					Match keyAttribMatch = _typeParsePattern.Match(keyAttrib.InnerText);
					
                    if (subNodeMatch.Success && keyAttribMatch.Success
							&& subNodeMatch.Result("$1") == keyAttribMatch.Result("$1") )
                    {
						matchNode = subNode; // Have type class match (ignoring assembly-name suffix)
					}
				}
			}
			
			return matchNode; //return last match if > 1
		}
	}
}
