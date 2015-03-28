#region MIT License

// Yamster!
// Copyright (c) Microsoft Corporation
// All rights reserved. 
// 
// MIT License
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
// associated documentation files (the ""Software""), to deal in the Software without restriction, 
// including without limitation the rights to use, copy, modify, merge, publish, distribute, 
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or 
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT 
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#endregion

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Linq;

namespace Yamster.Core
{
    public abstract class YqlPropertyNode : YqlObjectActionNode
    {
        #region class PropertyMapping
        protected class PropertyMapping
        {
            public Type DeclaringType { get; private set; }
            Dictionary<Enum, PropertyInfo> InfosById = new Dictionary<Enum,PropertyInfo>();

            public PropertyMapping(Type declaringType)
            {
                this.DeclaringType = declaringType;
            }

            public void Add(Enum id, PropertyInfo propertyInfo)
            {
                InfosById.Add(id, propertyInfo);
            }

            public PropertyInfo GetPropertyInfo(YqlPropertyNode node)
            {
                PropertyInfo propertyInfo;
                if (!InfosById.TryGetValue(node.AbstractPropertyId, out propertyInfo))
                    node.ReportError("No mapping was defined for the property " + node.AbstractPropertyId);
                return propertyInfo;
            }
        }
        #endregion

        protected readonly PropertyMapping Mapping;

        protected YqlPropertyNode(PropertyMapping mapping, YamsterModelType targetObjectType, YqlNode targetObject)
            : base(targetObjectType, targetObject)
        {
            this.Mapping = mapping;
        }

        public abstract Enum AbstractPropertyId { get; set; }

        public override string ToString()
        {
            var objectTypeName = this.Mapping.DeclaringType.Name;

            return "YqlNode: " + objectTypeName + " Property=" + AbstractPropertyId;
        }

        public override void CopyFrom(YqlNode source)
        {
            base.CopyFrom(source);
            var castedSource = (YqlPropertyNode) source;
            AbstractPropertyId = castedSource.AbstractPropertyId;
        }

        protected override void SaveXmlElement(XElement element)
        {
            element.Name = this.TargetObjectType.ToString() + "_" + this.AbstractPropertyId.ToString();

            base.SaveXmlElement(element);
        }

        protected override void LoadXmlElement(XElement element)
        {
            string elementName = element.Name.ToString();
            int underscoreIndex = elementName.IndexOf('_');
            if (underscoreIndex < -1)
                throw new InvalidOperationException("Unrecognized element name");
            string enumName = elementName.Substring(underscoreIndex+1);
            Type enumType = this.AbstractPropertyId.GetType();
            this.AbstractPropertyId = (Enum)Enum.Parse(enumType, enumName);

            base.LoadXmlElement(element);
        }

        internal static void RegisterXmlElements(YamsterModelType modelType,
            Type classType, Type enumType)
        {
            if (!typeof(YqlPropertyNode).IsAssignableFrom(classType))
                throw new ArgumentException("The classType must inherit from YqlPropertyNode");
            if (!enumType.IsEnum)
                throw new ArgumentException("The enumType must be an enum");

            foreach (Enum enumValue in Enum.GetValues(enumType))
            {
                // E.g. "Message_UserIdRepliedTo"
                string xmlName = modelType.ToString() + "_" + enumValue.ToString();
                YqlNode.RegisterXmlElement(classType, xmlName);
            }
        }

        protected internal override YqlCompiledNode CompileNode(YqlCompiler compiler)
        {
            var propertyInfo = Mapping.GetPropertyInfo(this);

            Type targetType = propertyInfo.DeclaringType;

            YqlCompiledNode[] compiledArgs;
            Expression targetExpression = CompileTargetExpression(compiler, out compiledArgs);

            Expression expression = Expression.Property(targetExpression, propertyInfo);
            return new YqlCompiledNode(this, expression, compiledArgs);
        }
    }

    public enum YqlThreadProperty
    {
        Id,
        SeenInInboxFeed,
        // TODO: When referenced from a Message, this broadens the view invalidation criteria
        Read
    }

    public class YqlThreadPropertyNode : YqlPropertyNode
    {
        #region Mapping
        static PropertyMapping ThreadMapping;

        static YqlThreadPropertyNode()
        {
            ThreadMapping = new PropertyMapping(typeof(YamsterThread));
            ThreadMapping.Add(YqlThreadProperty.Id, YamsterThread.Info_ThreadId);
            ThreadMapping.Add(YqlThreadProperty.SeenInInboxFeed, YamsterThread.Info_SeenInInboxFeed);
            ThreadMapping.Add(YqlThreadProperty.Read, YamsterThread.Info_Read);
        }

        #endregion

        public YqlThreadProperty PropertyId { get; set; }

        public YqlThreadPropertyNode()
            : base(ThreadMapping, YamsterModelType.Thread, null)
        {
        }
        public YqlThreadPropertyNode(YqlThreadProperty propertyId, YqlNode targetObject = null)
            : base(ThreadMapping, YamsterModelType.Thread, targetObject)
        {
            this.PropertyId = propertyId;
        }

        public override Enum AbstractPropertyId
        {
            get { return PropertyId; }
            set { PropertyId = (YqlThreadProperty)value; }
        }
    }

    public enum YqlMessageProperty
    {
        Id,
        Body,
        Starred,
        Sender,
        UserIdRepliedTo,
        NotifiedUsers,
        LikingUsers,
        Thread
    }

    public class YqlMessagePropertyNode : YqlPropertyNode
    {
        #region Mapping
        static PropertyMapping MessageMapping;

        static YqlMessagePropertyNode()
        {
            MessageMapping = new PropertyMapping(typeof(YamsterMessage));
            MessageMapping.Add(YqlMessageProperty.Id, YamsterMessage.Info_MessageId);
            MessageMapping.Add(YqlMessageProperty.Body, YamsterMessage.Info_Body);
            MessageMapping.Add(YqlMessageProperty.Starred, YamsterMessage.Info_Starred);
            MessageMapping.Add(YqlMessageProperty.Sender, YamsterMessage.Info_Sender);
            MessageMapping.Add(YqlMessageProperty.UserIdRepliedTo, YamsterMessage.Info_UserIdRepliedTo);
            MessageMapping.Add(YqlMessageProperty.NotifiedUsers, YamsterMessage.Info_NotifiedUsers);
            MessageMapping.Add(YqlMessageProperty.LikingUsers, YamsterMessage.Info_LikingUsers);
            MessageMapping.Add(YqlMessageProperty.Thread, YamsterMessage.Info_Thread);
        }

        #endregion

        public YqlMessageProperty PropertyId { get; set; }

        public YqlMessagePropertyNode()
            : base(MessageMapping, YamsterModelType.Message, null)
        {
        }
        public YqlMessagePropertyNode(YqlMessageProperty propertyId, YqlNode targetObject = null)
            : base(MessageMapping, YamsterModelType.Message, targetObject)
        {
            this.PropertyId = propertyId;
        }

        public override Enum AbstractPropertyId
        {
            get { return PropertyId; }
            set { PropertyId = (YqlMessageProperty) value; }
        }
    }

    public enum YqlUserProperty
    {
        // NOTE: If we add any properties that can change, then we need to
        // update YamsterMessageView to detect these properties
        Id
    }

    public class YqlUserPropertyNode : YqlPropertyNode
    {
        #region Mapping
        static PropertyMapping UserMapping;

        static YqlUserPropertyNode()
        {
            UserMapping = new PropertyMapping(typeof(YamsterUser));
            UserMapping.Add(YqlUserProperty.Id, YamsterUser.Info_UserId);
        }

        #endregion

        public YqlUserProperty PropertyId { get; set; }

        public YqlUserPropertyNode()
            : base(UserMapping, YamsterModelType.User, null)
        {
        }
        public YqlUserPropertyNode(YqlUserProperty propertyId, YqlNode targetObject = null)
            : base(UserMapping, YamsterModelType.User, targetObject)
        {
            this.PropertyId = propertyId;
        }

        public override Enum AbstractPropertyId
        {
            get { return PropertyId; }
            set { PropertyId = (YqlUserProperty) value; }
        }
    }

}
