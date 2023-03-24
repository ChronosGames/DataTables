﻿// ------------------------------------------------------------------------------
// <auto-generated>
//     此代码由工具生成。
//     运行时版本: 17.0.0.0
//  
//     对此文件的更改可能导致不正确的行为，如果
//     重新生成代码，这些更改将会丢失。
// </auto-generated>
// ------------------------------------------------------------------------------
namespace DataTables.GeneratorCore
{
    using System.Linq;
    using System.Text;
    using System.Collections.Generic;
    using System;
    
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public partial class DataRowTemplate : DataRowTemplateBase
    {
        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            this.Write("// <auto-generated />\r\n#pragma warning disable CS0105\r\n");
            this.Write(this.ToStringHelper.ToStringWithCulture(Using));
            this.Write("\r\nusing System;\r\nusing System.IO;\r\nusing System.Collections.Generic;\r\nusing DataT" +
                    "ables;\r\n\r\nnamespace ");
            this.Write(this.ToStringHelper.ToStringWithCulture(Namespace));
            this.Write("\r\n{\r\n    public sealed class DT");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.ClassName));
            this.Write(" : DataTable<");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.RealClassName));
            this.Write(">\r\n    {\r\n");

        for (var i = 0; i < GenerationContext.Indexs.Count; i++)
        {
            var item = GenerationContext.Indexs[i];

            this.Write("        private ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.GetIndexDictDefine(item)));
            this.Write(" m_Dict");
            this.Write(this.ToStringHelper.ToStringWithCulture(i + 1));
            this.Write(" = new ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.GetIndexDictDefine(item)));
            this.Write("();\r\n");
      }
        for (var i = 0; i < GenerationContext.Indexs.Count; i++)
        {
            var item = GenerationContext.Indexs[i];

            this.Write("        public ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.RealClassName));
            this.Write(" GetDataRow(");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.GetIndexsMethodDefine(item)));
            this.Write(")\r\n        {\r\n            return m_Dict");
            this.Write(this.ToStringHelper.ToStringWithCulture(i + 1));
            this.Write(".TryGetValue(");
            this.Write(this.ToStringHelper.ToStringWithCulture(string.Join(", ", item)));
            this.Write(", out var result) ? result : null;\r\n        }\r\n");
 } 
            this.Write("        protected override void InternalAddDataRow(");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.RealClassName));
            this.Write(" dataRow)\r\n        {\r\n            base.InternalAddDataRow(dataRow);\r\n");
  for (var i = 0; i < GenerationContext.Indexs.Count; i++)
    {
        var item = GenerationContext.Indexs[i];

            this.Write("            m_Dict");
            this.Write(this.ToStringHelper.ToStringWithCulture(i+1));
            this.Write(".Add(dataRow.");
            this.Write(this.ToStringHelper.ToStringWithCulture(string.Join(", dataRow.", item)));
            this.Write(", dataRow);\r\n");
  } 
            this.Write("        }\r\n    }\r\n\r\n    /// <summary>");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.Title));
            this.Write("</summary>\r\n    public sealed class ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GenerationContext.RealClassName));
            this.Write(" : DataRowBase\r\n    {\r\n");
 foreach(var item in GenerationContext.Properties) { 
            this.Write("        /// <summary>");
            this.Write(this.ToStringHelper.ToStringWithCulture(item.Comment));
            this.Write("</summary>\r\n        public ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GetPropertyTypeString(item)));
            this.Write(" ");
            this.Write(this.ToStringHelper.ToStringWithCulture(item.Name));
            this.Write(" { get; private set; }\r\n");
 } 
            this.Write("\r\n        public override bool Deserialize(BinaryReader reader)\r\n        {\r\n");
 foreach(var item in GenerationContext.Properties) { 
            this.Write("                    ");
            this.Write(this.ToStringHelper.ToStringWithCulture(GetDeserializeMethodString(item)));
            this.Write("\r\n");
 } 
            this.Write("            return true;\r\n        }\r\n    }\r\n}");
            return this.GenerationEnvironment.ToString();
        }
    }
    #region Base class
    /// <summary>
    /// Base class for this transformation
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.TextTemplating", "17.0.0.0")]
    public class DataRowTemplateBase
    {
        #region Fields
        private global::System.Text.StringBuilder generationEnvironmentField;
        private global::System.CodeDom.Compiler.CompilerErrorCollection errorsField;
        private global::System.Collections.Generic.List<int> indentLengthsField;
        private string currentIndentField = "";
        private bool endsWithNewline;
        private global::System.Collections.Generic.IDictionary<string, object> sessionField;
        #endregion
        #region Properties
        /// <summary>
        /// The string builder that generation-time code is using to assemble generated output
        /// </summary>
        protected System.Text.StringBuilder GenerationEnvironment
        {
            get
            {
                if ((this.generationEnvironmentField == null))
                {
                    this.generationEnvironmentField = new global::System.Text.StringBuilder();
                }
                return this.generationEnvironmentField;
            }
            set
            {
                this.generationEnvironmentField = value;
            }
        }
        /// <summary>
        /// The error collection for the generation process
        /// </summary>
        public System.CodeDom.Compiler.CompilerErrorCollection Errors
        {
            get
            {
                if ((this.errorsField == null))
                {
                    this.errorsField = new global::System.CodeDom.Compiler.CompilerErrorCollection();
                }
                return this.errorsField;
            }
        }
        /// <summary>
        /// A list of the lengths of each indent that was added with PushIndent
        /// </summary>
        private System.Collections.Generic.List<int> indentLengths
        {
            get
            {
                if ((this.indentLengthsField == null))
                {
                    this.indentLengthsField = new global::System.Collections.Generic.List<int>();
                }
                return this.indentLengthsField;
            }
        }
        /// <summary>
        /// Gets the current indent we use when adding lines to the output
        /// </summary>
        public string CurrentIndent
        {
            get
            {
                return this.currentIndentField;
            }
        }
        /// <summary>
        /// Current transformation session
        /// </summary>
        public virtual global::System.Collections.Generic.IDictionary<string, object> Session
        {
            get
            {
                return this.sessionField;
            }
            set
            {
                this.sessionField = value;
            }
        }
        #endregion
        #region Transform-time helpers
        /// <summary>
        /// Write text directly into the generated output
        /// </summary>
        public void Write(string textToAppend)
        {
            if (string.IsNullOrEmpty(textToAppend))
            {
                return;
            }
            // If we're starting off, or if the previous text ended with a newline,
            // we have to append the current indent first.
            if (((this.GenerationEnvironment.Length == 0) 
                        || this.endsWithNewline))
            {
                this.GenerationEnvironment.Append(this.currentIndentField);
                this.endsWithNewline = false;
            }
            // Check if the current text ends with a newline
            if (textToAppend.EndsWith(global::System.Environment.NewLine, global::System.StringComparison.CurrentCulture))
            {
                this.endsWithNewline = true;
            }
            // This is an optimization. If the current indent is "", then we don't have to do any
            // of the more complex stuff further down.
            if ((this.currentIndentField.Length == 0))
            {
                this.GenerationEnvironment.Append(textToAppend);
                return;
            }
            // Everywhere there is a newline in the text, add an indent after it
            textToAppend = textToAppend.Replace(global::System.Environment.NewLine, (global::System.Environment.NewLine + this.currentIndentField));
            // If the text ends with a newline, then we should strip off the indent added at the very end
            // because the appropriate indent will be added when the next time Write() is called
            if (this.endsWithNewline)
            {
                this.GenerationEnvironment.Append(textToAppend, 0, (textToAppend.Length - this.currentIndentField.Length));
            }
            else
            {
                this.GenerationEnvironment.Append(textToAppend);
            }
        }
        /// <summary>
        /// Write text directly into the generated output
        /// </summary>
        public void WriteLine(string textToAppend)
        {
            this.Write(textToAppend);
            this.GenerationEnvironment.AppendLine();
            this.endsWithNewline = true;
        }
        /// <summary>
        /// Write formatted text directly into the generated output
        /// </summary>
        public void Write(string format, params object[] args)
        {
            this.Write(string.Format(global::System.Globalization.CultureInfo.CurrentCulture, format, args));
        }
        /// <summary>
        /// Write formatted text directly into the generated output
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            this.WriteLine(string.Format(global::System.Globalization.CultureInfo.CurrentCulture, format, args));
        }
        /// <summary>
        /// Raise an error
        /// </summary>
        public void Error(string message)
        {
            System.CodeDom.Compiler.CompilerError error = new global::System.CodeDom.Compiler.CompilerError();
            error.ErrorText = message;
            this.Errors.Add(error);
        }
        /// <summary>
        /// Raise a warning
        /// </summary>
        public void Warning(string message)
        {
            System.CodeDom.Compiler.CompilerError error = new global::System.CodeDom.Compiler.CompilerError();
            error.ErrorText = message;
            error.IsWarning = true;
            this.Errors.Add(error);
        }
        /// <summary>
        /// Increase the indent
        /// </summary>
        public void PushIndent(string indent)
        {
            if ((indent == null))
            {
                throw new global::System.ArgumentNullException("indent");
            }
            this.currentIndentField = (this.currentIndentField + indent);
            this.indentLengths.Add(indent.Length);
        }
        /// <summary>
        /// Remove the last indent that was added with PushIndent
        /// </summary>
        public string PopIndent()
        {
            string returnValue = "";
            if ((this.indentLengths.Count > 0))
            {
                int indentLength = this.indentLengths[(this.indentLengths.Count - 1)];
                this.indentLengths.RemoveAt((this.indentLengths.Count - 1));
                if ((indentLength > 0))
                {
                    returnValue = this.currentIndentField.Substring((this.currentIndentField.Length - indentLength));
                    this.currentIndentField = this.currentIndentField.Remove((this.currentIndentField.Length - indentLength));
                }
            }
            return returnValue;
        }
        /// <summary>
        /// Remove any indentation
        /// </summary>
        public void ClearIndent()
        {
            this.indentLengths.Clear();
            this.currentIndentField = "";
        }
        #endregion
        #region ToString Helpers
        /// <summary>
        /// Utility class to produce culture-oriented representation of an object as a string.
        /// </summary>
        public class ToStringInstanceHelper
        {
            private System.IFormatProvider formatProviderField  = global::System.Globalization.CultureInfo.InvariantCulture;
            /// <summary>
            /// Gets or sets format provider to be used by ToStringWithCulture method.
            /// </summary>
            public System.IFormatProvider FormatProvider
            {
                get
                {
                    return this.formatProviderField ;
                }
                set
                {
                    if ((value != null))
                    {
                        this.formatProviderField  = value;
                    }
                }
            }
            /// <summary>
            /// This is called from the compile/run appdomain to convert objects within an expression block to a string
            /// </summary>
            public string ToStringWithCulture(object objectToConvert)
            {
                if ((objectToConvert == null))
                {
                    throw new global::System.ArgumentNullException("objectToConvert");
                }
                System.Type t = objectToConvert.GetType();
                System.Reflection.MethodInfo method = t.GetMethod("ToString", new System.Type[] {
                            typeof(System.IFormatProvider)});
                if ((method == null))
                {
                    return objectToConvert.ToString();
                }
                else
                {
                    return ((string)(method.Invoke(objectToConvert, new object[] {
                                this.formatProviderField })));
                }
            }
        }
        private ToStringInstanceHelper toStringHelperField = new ToStringInstanceHelper();
        /// <summary>
        /// Helper to produce culture-oriented representation of an object as a string
        /// </summary>
        public ToStringInstanceHelper ToStringHelper
        {
            get
            {
                return this.toStringHelperField;
            }
        }
        #endregion
    }
    #endregion
}
