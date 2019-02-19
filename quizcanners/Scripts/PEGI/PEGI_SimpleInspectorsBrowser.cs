﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;
using System;

namespace QuizCannersUtilities
{

    [ExecuteInEditMode]
    public class PEGI_SimpleInspectorsBrowser : ComponentStd, IPEGI, IKeepMyStd
    {

        public List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [SerializeField] string stdData = "";

        public string ConfigStd { get { return stdData; } set { stdData = value; } }

        void OnEnable() => this.LoadStdData();

        void OnDisable() => this.SaveStdData();

        public override bool Decode(string tg, string data)
        {
            switch (tg)
            {
                case "ld": referencesMeta.Decode(data); break;
                default: return false;
            }
            return true;
        }

        public override StdEncoder Encode() =>
            this.EncodeUnrecognized().Add("ld", referencesMeta);

        #region Inspector
#if PEGI
        public override bool Inspect()
        {
            bool changed = base.Inspect();
            referencesMeta.enter_List_UObj(ref objects, ref inspectedStuff, 3);
            return changed;
        }
#endif
        #endregion
    }

}