﻿using System.IO;
using Newtonsoft.Json;
using Secs4Net;
using Secs4Net.Json;

namespace SecsMessageVisuallizer.ViewModel
{
    public class SecsMessageViewModel : TreeViewItemViewModel
    {
        readonly SecsMessage _secsMsg;
        public SecsMessageViewModel(SecsMessage secsMsg)
            : base(null, false)
        {
            _secsMsg = secsMsg;
            if (secsMsg.SecsItem != null)
                base.Children.Add(new SecsItemViewModel(secsMsg.SecsItem, this));
        }

        public byte StreamNumber => _secsMsg.S;
        public byte FunctionNumber => _secsMsg.F;
        public string Name
        {
            get { return _secsMsg.Name; }
            set
            {
                if (value != null && value != _secsMsg.Name)
                {
                    _secsMsg.Name = value;
                    base.OnPropertyChanged();
                }
            }
        }
        public bool ReplyExpected => _secsMsg.ReplyExpected;

        public override string ToString()
        {
            using (var s = new StringWriter())
            using (var j = new JsonTextWriter(s) { Formatting = Formatting.Indented })
            {
                _secsMsg.WriteTo(j);
                j.Flush();
                return s.ToString();
            }
        }
    }
}
