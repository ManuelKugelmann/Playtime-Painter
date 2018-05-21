﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System;
using PlayerAndEditorGUI;
using SharedTools_Stuff;
using STD_Logic;

namespace StoryTriggerData{

    [TagName(Page.storyTag)]
    [ExecuteInEditMode]
    public class Page : STD_Poolable {

        List<STD_Poolable> linkedObjects;

        public const string storyTag = "page";

        [NonSerialized]
        public string anotherBook;

        [NonSerialized]
        public float sceneScale;
        public UniversePosition sPOS = new UniversePosition();
        public UniverseLength uReach = new UniverseLength();
        public UniverseLength uSize = new UniverseLength(1);
        bool objectsLoaded;
        public bool noClamping;
        public string OriginBook;

        public override string getDefaultTagName() {
            return storyTag;
        }

        public string GerResourcePath() {
            if ((anotherBook != null) && (anotherBook.Length > 0))
                return anotherBook;  // Another book or web address

            if (parentPage == null)
                return OriginBook; // this book
            else
                return parentPage.GerResourcePath() + "/" + parentPage.gameObject.name;
        }

        public override void Reboot() {
            if (poolController == null)
                myPoolController.AddToPool(this.gameObject);

             gameObject.hideFlags = HideFlags.DontSave;
            //gameObject.AddFlagsOnItAndChildren(HideFlags.DontSave);

            anotherBook = "";

            linkedObjects = new List<STD_Poolable>();

            noClamping = false;

            sPOS = new UniversePosition();
            uSize = new UniverseLength(1);
            uReach = new UniverseLength(10);

            transform.localScale = Vector3.one;

            objectsLoaded = false;
        }

        public override bool Decode(string tag, string data) {

            switch (tag) {
                case "name": gameObject.name = data; break;
                case "origin": OriginBook = data; break;
                case "URL": anotherBook = data; break;
                case "size": uSize = new UniverseLength(data); break;
                case "radius": uReach = new UniverseLength(data); break;
                case UniversePosition.storyTag: sPOS.Decode(data); break;
                case "noClamp": noClamping = data.ToBool(); break;
                default:
                    STD_Poolable storyObject = STD_Pool.getOne(tag);

                    if (storyObject != null)
                        data.DecodeInto(storyObject.LinkTo(this));
                    else
                        return false; break;
            }
            return true;
        }

        void encodeMeta(stdEncoder cody) {
            cody.AddText("origin", OriginBook);
            cody.AddText("name", gameObject.name);
            cody.AddIfNotEmpty("URL", anotherBook);
            cody.Add(UniversePosition.storyTag,sPOS);
            if (!uSize.Equals(10))
                cody.Add("size", uSize);
            if (!uReach.Equals(1))
                cody.Add("radius", uReach);
            if (noClamping) cody.Add("noClamp", noClamping);
        }

        public override stdEncoder Encode() { // Page and it's full content is saved in a saparate file

            var cody = new stdEncoder();

            encodeMeta(cody);

            return cody;
        }

        public stdEncoder EncodeContent() {
            var cody = new stdEncoder();

            foreach (STD_Poolable sc in linkedObjects)
                cody.Add(sc.getDefaultTagName(),sc.Encode());

            return cody;
        }

        public void SavePageContent() {
            if (objectsLoaded)
                ResourceSaver.SaveToResources(TriggerGroup.StoriesFolderName, GerResourcePath(), gameObject.name, EncodeContent().ToString());
        }

        public void LoadContent() {
            if (!objectsLoaded)
                new stdDecoder(ResourceLoader.LoadStoryFromResource(TriggerGroup.StoriesFolderName, GerResourcePath(), gameObject.name)).DecodeTagsFor(this);
            objectsLoaded = true;
        }

        public void RenamePage(string newName) {
#if UNITY_EDITOR
            bool duplicate = false;
            foreach (Page p in Book.HOMEpages) {
                if ((p.gameObject.name == gameObject.name) && (p.gameObject != this.gameObject)) { duplicate = true; break; } //
            }

            string path = "Assets/" + TriggerGroup.StoriesFolderName + "/Resources/" + GerResourcePath() + "/";

            if (duplicate)
                UnityHelperFunctions.DuplicateResource(TriggerGroup.StoriesFolderName, GerResourcePath(), gameObject.name, newName);
            else
                UnityEditor.AssetDatabase.RenameAsset(path + gameObject.name + ResourceSaver.fileType, newName + ResourceSaver.fileType);

            gameObject.name = newName;
#endif
        }

        public override void Deactivate() {

            if (Application.isPlaying == false)
                SavePageContent();

            clearPage();
            if (parentPage == null)
                Book.HOMEpages.Remove(this);
            base.Deactivate();
        }

        public void Unlink(STD_Poolable sb) {
            if (linkedObjects.Remove(sb)) {
                sb.parentPage = null;
                if (!STD_Pool.DestroyingAll)
                    sb.transform.parent = null;
            }
        }

        public void Link(STD_Poolable sb) {
            if (sb.parentPage != null)
                sb.parentPage.Unlink(sb);
            sb.transform.parent = transform;
            sb.parentPage = this;
            linkedObjects.Add(sb);
        }

        public void clearPage() {
            for (int i = linkedObjects.Count - 1; i >= 0; i--)
                linkedObjects[i].Deactivate();

            linkedObjects = new List<STD_Poolable>();

            objectsLoaded = false;
        }
#if !NO_PEGI
        public static Page browsedPage;
        int exploredObject = -1;

        static bool notsafeCFG = false;
        public override bool PEGI() {

            bool changed = false;

            browsedPage = this;

            if (exploredObject >= 0) {

                if ((linkedObjects.Count <= exploredObject) || ("< Pools".Click(35)))
                    exploredObject = -1;
                else
                    linkedObjects[exploredObject].PEGI();

            }

            if (exploredObject == -1) {

                pegi.newLine();

                if (parentPage == null)
                    pegi.write(gameObject.name + ":HOME page ");
                else {
                    pegi.write(gameObject.name + " is child of:", 60);
                    pegi.write(parentPage);
                }

                (objectsLoaded ? "loaded" : "not loaded").nl(60);


                if ("Location: ".edit(60, ref anotherBook).nl()) {
                    if (anotherBook[anotherBook.Length - 1] == '/')
                        anotherBook = anotherBook.Substring(0, anotherBook.Length - 1);
                }


                if ("config".foldout(ref notsafeCFG)) {

                    if ("Clear".Click().nl())
                        clearPage();

                    if ("Don't scale: ".toggle(ref noClamping).nl())
                        transform.localScale = Vector3.one;

                }

                pegi.newLine();

                if ((!objectsLoaded) && ("Load".Click()))
                    Book.instBook.lerpTarget = this; 
                
                if ((objectsLoaded) && ("Save".Click()))
                    SavePageContent();

                pegi.newLine();

                if (objectsLoaded)
                    for (int i = 0; i < STD_Pool.all.Length; i++) {
                        STD_Pool up = STD_Pool.all[i];

                        pegi.write(up.storyTag, 35);
                        pegi.write(up.pool.prefab);

                        if (icon.Add.Click(20))
                            STD_Pool.all[i].pool.getFreeGO().GetComponent<STD_Poolable>().LinkTo(this).Decode(null);

                        int Delete = -1;

                        for (int o = 0; o < linkedObjects.Count; o++) {

                            STD_Poolable obj = linkedObjects[o];
                            if (obj.poolController == up.pool) {
                                pegi.newLine();

                                if (icon.Delete.Click(20))
                                    Delete = o;

                                pegi.edit(linkedObjects[o].gameObject);

                                if (pegi.Click(icon.Edit, "Edit object", 20))
                                    exploredObject = o;
                            }
                        }

                        if (Delete != -1)
                            linkedObjects[Delete].Deactivate();

                        pegi.newLine();
                    }

                if (sPOS.ExtendedPEGI(uSize, uReach))
                    PostPositionUpdate();
            }
            pegi.newLine();
            return changed;

        }

#endif
        void OnDrawGizmosSelected() {
            if (!objectsLoaded) {
                UniverseLength rng = SpaceValues.tmpRange;
                rng.CopyFrom(uReach).Divide(SpaceValues.universeScale).MultiplyBy(scale);
              
                Gizmos.DrawWireSphere(this.transform.position, rng.Meters+rng.KM*SpaceValues.meters_In_Kilometer);
            }
        }

        float scale;
        UniverseLength dist = new UniverseLength();
     
        public override void PostPositionUpdate() {



            if (noClamping)
                transform.position = sPOS.ToV3unclamped(uReach);
            else {
                transform.position = sPOS.ToV3(uSize, uReach, out scale);
                transform.localScale = Vector3.one * scale;
            }

            if (!UniversePosition.isInReach)
                dist.CopyFrom(SpaceValues.tmpDist);

            if ((!objectsLoaded) && (UniversePosition.isInReach))
                LoadContent();
            else if (objectsLoaded && (!UniversePosition.isInReach)) {
                if (Application.isPlaying == false)
                    SavePageContent();
                clearPage();
            }



            for (int i = linkedObjects.Count - 1; i >= 0; i--)
                linkedObjects[i].PostPositionUpdate();
        }

        public static PoolController<Page> myPoolController;

        public override void SetStaticPoolController(STD_Pool inst) {
            myPoolController = (PoolController<Page>)inst.pool;
        }
       
}
}