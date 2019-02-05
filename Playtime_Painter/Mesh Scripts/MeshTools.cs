﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayerAndEditorGUI;

using QuizCannersUtilities;

namespace Playtime_Painter
{

    public class MeshToolBase : PainterStuff_STD, IPEGI, IGotDisplayName
    {

        public delegate bool meshToolPlugBool(MeshToolBase tool, out bool val);
        public static meshToolPlugBool showVerticesPlugs;

        protected static bool Dirty { get { return EditedMesh.Dirty; } set { EditedMesh.Dirty = value; } }

        static List<MeshToolBase> _allTools;

        public static List<MeshToolBase> AllTools
        {
            get
            {
                if (_allTools == null && !applicationIsQuitting)
                {
                    _allTools = new List<MeshToolBase>();

                    var tps = CsharpFuncs.GetAllChildTypesOf<MeshToolBase>();

                    foreach (var t in tps)
                        _allTools.Add((MeshToolBase)Activator.CreateInstance(t));

                    /*_allTools.Add(new VertexPositionTool());
                    _allTools.Add(new SharpFacesTool());
                    _allTools.Add(new VertexColorTool());
                    _allTools.Add(new VertexEdgeTool());
                    _allTools.Add(new TriangleAtlasTool());
                    _allTools.Add(new TriangleSubmeshTool());
                    _allTools.Add(new VertexShadowTool());*/
                }
                return _allTools;
            }
        }

        //protected MeshManager mgmt { get { return MeshManager.inst; } }
        // protected PainterDataAndConfig cfg { get { return PainterDataAndConfig.dataHolder; } }
        protected LineData PointedLine => MeshMGMT.PointedLine;
        protected Triangle PointedTris => MeshMGMT.PointedTris;
        protected Triangle SelectedTris => MeshMGMT.SelectedTris;
        protected Vertex PointedUV => MeshMGMT.PointedUV;
        protected Vertex SelectedUV => MeshMGMT.SelectedUV;
        protected MeshPoint PointedVertex => MeshMGMT.PointedUV.meshPoint;
        protected EditableMesh FreshPreviewMesh
        {
            get
            {
                if (!MeshMGMT.previewMesh)
                {
                    MeshMGMT.previewEdMesh = new EditableMesh();
                    MeshMGMT.previewEdMesh.Decode(MeshMGMT.editedMesh.Encode().ToString());
                }
                return MeshMGMT.previewEdMesh;
            }
        }


        public virtual bool ShowVerticesDefault => true;

        public bool ShowVertices
        {
            get
            {
                if (showVerticesPlugs != null)
                {
                    bool val;
                    foreach (meshToolPlugBool p in showVerticesPlugs.GetInvocationList())
                        if (p(this, out val))
                            return val;
                }
                return ShowVerticesDefault;
            }
        }

        public virtual bool ShowLines => true;
        public virtual bool ShowTriangles => true;
        public virtual bool ShowGrid => false;

        public virtual bool ShowSelectedVerticle => false;
        public virtual bool ShowSelectedLine => false;
        public virtual bool ShowSelectedTriangle => false;

        public virtual Color VertColor => Color.gray;

        public virtual void OnSelectTool() { }

        public virtual void OnDeSelectTool() { }

        public virtual void OnGridChange() { }

        public virtual void AssignText(MarkerWithText mrkr, MeshPoint vpoint) => mrkr.textm.text = "";

        public virtual bool MouseEventPointedVertex() => false;

        public virtual bool MouseEventPointedLine() => false;

        public virtual bool MouseEventPointedTriangle() => false;

        public virtual void MouseEventPointedNothing() { }

        public virtual void KeysEventPointedVertex() { }

        public virtual void KeysEventDragging() { }

        public virtual void KeysEventPointedLine() { }

        public virtual void KeysEventPointedTriangle() { }

        public virtual void ManageDragging() { }

        #region Encode & Decode
        public override StdEncoder Encode() => null;

        public override bool Decode(string tag, string data) => false;
        #endregion

        #region Inspector
        public virtual string Tooltip => "No tooltippp";

        public virtual string NameForPEGIdisplay => " No Name ";

        public virtual bool Inspect() => false;
        #endregion
    }

    public class VertexPositionTool : MeshToolBase
    {
        public static VertexPositionTool inst;

        public VertexPositionTool()
        {
            inst = this;
        }

        public bool addToTrianglesAndLines = false;

        List<MeshPoint> draggedVertices = new List<MeshPoint>();
        Vector3 originalPosition;

        public override bool ShowGrid => true;

        public override void AssignText(MarkerWithText mrkr, MeshPoint vpoint)
        {

            var pvrt = MeshMGMT.GetSelectedVert();

            if ((vpoint.uvpoints.Count > 1) || (pvrt == vpoint))
            {

                //Texture tex = meshMGMT.target.GetTextureOnMaterial();//meshRenderer.sharedMaterial.mainTexture;

                if (pvrt == vpoint)
                {
                    mrkr.textm.text = (vpoint.uvpoints.Count > 1) ? ((vpoint.uvpoints.IndexOf(MeshMGMT.SelectedUV) + 1).ToString() + "/" + vpoint.uvpoints.Count.ToString() +
                        (vpoint.SmoothNormal ? "s" : "")) : "";


                }
                else
                    mrkr.textm.text = vpoint.uvpoints.Count.ToString() +
                        (vpoint.SmoothNormal ? "s" : "");
            }
            else mrkr.textm.text = "";
        }

        public override Color VertColor => Color.white;

        #region Inspector
        public override string Tooltip =>

                    "Alt - Raycast To Grid" + Environment.NewLine +
                    "LMB - Add Vertices/Make Triangles (Go Clockwise), Drag" + Environment.NewLine +
                    "Scroll - Change Plane" + Environment.NewLine +
                    "U - make triengle unique." + Environment.NewLine +
                    "M - merge with nearest while dragging ";

        public override string NameForPEGIdisplay => "ADD & MOVE";

#if PEGI

        Vector3 offset;
        public override bool Inspect()
        {

            bool changed = false;

            MeshManager mgm = MeshMGMT;

            PainterDataAndConfig sd = TexMGMTdata;

            if (mgm.MeshTool.ShowGrid)
            {
                "Snap to grid:".toggleIcon(ref sd.SnapToGrid).nl();

                if (sd.SnapToGrid)
                    "size:".edit(40, ref sd.SnapToGridSize);
            }

            pegi.nl();

            "Pixel-Perfect".toggleIcon("New vertex will have UV coordinate rounded to half a pixel.", ref Cfg.pixelPerfectMeshEditing).nl();

            "Add into mesh".toggleIcon("Will split triangles and edges by inserting vertices", ref addToTrianglesAndLines).nl();

            "Add Smooth:".toggle(70, ref Cfg.newVerticesSmooth);
            if ("Sharp All".Click())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = false;
                mgm.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = false;
            }

            if ("Smooth All".Click().nl())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = true;
                mgm.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = true;
            }

            "Add Unique:".toggleIcon(ref Cfg.newVerticesUnique);
            if ("All shared".Click())
            {
                mgm.editedMesh.AllVerticesShared();
                mgm.editedMesh.Dirty = true;
                Cfg.newVerticesUnique = false;
            }

            if ("All unique".Click().nl())
            {
                foreach (Triangle t in EditedMesh.triangles)
                    mgm.editedMesh.GiveTriangleUniqueVerticles(t);
                mgm.editedMesh.Dirty = true;
                Cfg.newVerticesUnique = true;
            }

            if ("Auto Bevel".Click())
                SharpFacesTool.inst.AutoAssignDominantNormalsForBeveling();
            "Sensitivity".edit(60, ref Cfg.bevelDetectionSensetivity, 3, 30).nl();

            if ("Offset".foldout())
            {
                "center".edit(ref offset).nl();
                if ("Modify".Click().nl())
                {
                    foreach (var v in EditedMesh.meshPoints)
                        v.localPos += offset;

                    offset = -offset;

                    Dirty = true;

                }

                if ("Auto Center".Click().nl())
                {
                    Vector3 avr = Vector3.zero;
                    foreach (var v in EditedMesh.meshPoints)
                        avr += v.localPos;

                    offset = -avr / EditedMesh.meshPoints.Count;
                }

            }

            /*  if ("Mirror by Center".Click()) {
                  GridNavigator.onGridPos = mgm.target.transform.position;
                  mgm.UpdateLocalSpaceV3s();
                  mgm.editedMesh.MirrorVerticlesAgainsThePlane(mgm.onGridLocal);
              }

              if (pegi.Click("Mirror by Plane")) {
                  mgm.UpdateLocalSpaceV3s();
                  mgm.editedMesh.MirrorVerticlesAgainsThePlane(mgm.onGridLocal);
              }
              pegi.newLine();

              pegi.edit(ref displace);
              pegi.newLine();

              if (pegi.Click("Cancel")) displace = Vector3.zero;

              if (pegi.Click("Apply")) {
                  mgm.edMesh.Displace(displace);
                  mgm.edMesh.dirty = true;
                  displace = Vector3.zero;
              }
              */

            pegi.newLine();

            return changed;
        }
#endif
        #endregion

        #region Kyboard
        public override void KeysEventDragging()
        {
            var m = MeshMGMT;
            if ((KeyCode.M.IsDown()) && (m.SelectedUV != null))
            {
                m.SelectedUV.meshPoint.MergeWithNearest();
                m.Dragging = false;
                m.editedMesh.Dirty = true;
#if PEGI
                "M - merge with nearest".TeachingNotification();
#endif
            }
        }

        public override void KeysEventPointedVertex()
        {

            var m = MeshMGMT;

            if (KeyCode.Delete.IsDown())
            {
                //Debug.Log("Deleting");
                if (!EditorInputManager.Control)
                {
                    if (m.PointedUV.meshPoint.uvpoints.Count == 1)
                    {
                        if (!m.DeleteVertHEAL(MeshMGMT.PointedUV.meshPoint))
                            m.DeleteUv(MeshMGMT.PointedUV);
                    }
                    else
                        while (m.PointedUV.meshPoint.uvpoints.Count > 1)
                            m.editedMesh.MoveTris(m.PointedUV.meshPoint.uvpoints[1], m.PointedUV.meshPoint.uvpoints[0]);


                }
                else
                    m.DeleteUv(m.PointedUV);
                m.editedMesh.Dirty = true;
            }
        }

        public override void KeysEventPointedTriangle()
        {
            if (KeyCode.Backspace.IsDown() || KeyCode.Delete.IsDown())
            {
                MeshMGMT.editedMesh.triangles.Remove(PointedTris);
                foreach (var uv in PointedTris.vertexes)
                    if (uv.meshPoint.uvpoints.Count == 1 && uv.tris.Count == 1)
                        EditedMesh.meshPoints.Remove(uv.meshPoint);

                MeshMGMT.editedMesh.Dirty = true;
                return;
            }

            if (KeyCode.U.IsDown())
                PointedTris.MakeTriangleVertUnique(PointedUV);

            /*  if (KeyCode.N.isDown())
              {

                  if (!EditorInputManager.getAltKey())
                  {
                      int no = pointedTris.NumberOf(pointedTris.GetClosestTo(meshMGMT.collisionPosLocal));
                      pointedTris.SharpCorner[no] = !pointedTris.SharpCorner[no];

                      (pointedTris.SharpCorner[no] ? "Triangle edge's Normal is now dominant" : "Triangle edge Normal is NO longer dominant").TeachingNotification();
                  }
                  else
                  {
                      pointedTris.InvertNormal();
                      "Inverting Normals".TeachingNotification();
                  }

                  meshMGMT.edMesh.dirty = true;

              }*/


        }
        #endregion

        #region Mouse
        public override bool MouseEventPointedVertex()
        {

            if (EditorInputManager.GetMouseButtonDown(0))
            {
                var m = MeshMGMT;

                m.Dragging = true;
                m.AssignSelected(m.PointedUV);
                originalPosition = PointedUV.meshPoint.WorldPos;
                draggedVertices.Clear();
                draggedVertices.Add(PointedUV.meshPoint);


            }

            return false;

        }

        public override bool MouseEventPointedLine()
        {

            var m = MeshMGMT;

            if (EditorInputManager.GetMouseButtonDown(0))
            {
                m.Dragging = true;
                originalPosition = GridNavigator.collisionPos;
                GridNavigator.onGridPos = GridNavigator.collisionPos;
                draggedVertices.Clear();
                foreach (var uv in PointedLine.pnts)
                    draggedVertices.Add(uv.meshPoint);
            }

            if (EditorInputManager.GetMouseButtonUp(0))
            {
                if (addToTrianglesAndLines && m.DragDelay > 0 && draggedVertices.Contains(PointedLine))
                    MeshMGMT.editedMesh.InsertIntoLine(MeshMGMT.PointedLine.pnts[0].meshPoint, MeshMGMT.PointedLine.pnts[1].meshPoint, MeshMGMT.collisionPosLocal);
            }

            return false;
        }

        public override bool MouseEventPointedTriangle()
        {
            var m = MeshMGMT;

            if (EditorInputManager.GetMouseButtonDown(0))
            {

                m.Dragging = true;
                originalPosition = GridNavigator.collisionPos;
                GridNavigator.onGridPos = GridNavigator.collisionPos;
                draggedVertices.Clear();
                foreach (var uv in PointedTris.vertexes)
                    draggedVertices.Add(uv.meshPoint);

            }

            if (addToTrianglesAndLines && EditorInputManager.GetMouseButtonUp(0) && m.DragDelay > 0 && draggedVertices.Contains(PointedTris))
            {
                if (Cfg.newVerticesUnique)
                    m.editedMesh.InsertIntoTriangleUniqueVerticles(m.PointedTris, m.collisionPosLocal);
                else
                    m.editedMesh.InsertIntoTriangle(m.PointedTris, m.collisionPosLocal);
            }

            return false;
        }

        public override void MouseEventPointedNothing()
        {
            if (EditorInputManager.GetMouseButtonDown(0))
                MeshMGMT.AddPoint(MeshMGMT.onGridLocal);
        }

        public override void ManageDragging()
        {
            var m = MeshMGMT;

            bool beforeCouldDrag = m.DragDelay <= 0;

            if (EditorInputManager.GetMouseButtonUp(0) || !EditorInputManager.GetMouseButton(0))
            {
                m.Dragging = false;

                if (beforeCouldDrag)
                    EditedMesh.dirty_Position = true;
                else if (m.TrisVerts < 3 && m.SelectedUV != null && !m.IsInTrisSet(m.SelectedUV.meshPoint))
                    m.AddToTrisSet(m.SelectedUV);

            }
            else
            {
               
                bool canDrag = m.DragDelay <= 0;

                if (beforeCouldDrag != canDrag && EditorInputManager.Alt && m.SelectedUV.meshPoint.uvpoints.Count > 1)
                    m.DisconnectDragged();

                if (canDrag && GridNavigator.Inst().AngGridToCamera(GridNavigator.onGridPos) < 82)
                {

                    Vector3 delta = GridNavigator.onGridPos - originalPosition;

                    if (delta.magnitude > 0)
                    {

                        m.TrisVerts = 0;

                        foreach (var v in draggedVertices)
                            v.WorldPos += delta;

                        originalPosition = GridNavigator.onGridPos;
                    }
                }
            }
        }
        #endregion

        #region Encode & Decode
        public override StdEncoder Encode() => new StdEncoder()
            .Add_Bool("inM", addToTrianglesAndLines);
        
        public override bool Decode(string tag, string data)
        {
            switch (tag)
            {
                case "inM": addToTrianglesAndLines = data.ToBool(); break;
                default: return false;
            }
            return true;
        }
        #endregion

    }

    public class SharpFacesTool : MeshToolBase
    {
        public static SharpFacesTool inst;

        public SharpFacesTool()
        {
            inst = this;
        }

        public bool SetTo = true;

        public override bool ShowVerticesDefault { get { return false; } }

        #region Inspector
        public override string NameForPEGIdisplay => "Dominant Faces";

        public override string Tooltip
        {
            get
            {
                return "Paint the DOMINANCE on triangles" + Environment.NewLine +
                    "It will affect how normal vector will be calculated" + Environment.NewLine +
                    "N - smooth verticle, detect edge" + Environment.NewLine +
                    "N on triangle near vertex - replace smooth normal of this vertex with This triangle's normal" + Environment.NewLine +
                    "N on line to ForceNormal on connected triangles. (Alt - unforce)"
                    ;
            }
        }
#if PEGI
        public override bool Inspect()
        {

            MeshManager m = MeshMGMT;

            "Dominance True".toggle(ref SetTo).nl();

            pegi.ClickToEditScript();

            if ("Auto Bevel".Click())
                AutoAssignDominantNormalsForBeveling();
            "Sensitivity".edit(60, ref Cfg.bevelDetectionSensetivity, 3, 30).nl();

            if ("Sharp All".Click())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = false;
                m.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = false;
            }

            if ("Smooth All".Click().nl())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = true;
                m.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = true;
            }


            return false;

        }
#endif
        #endregion

        public override bool MouseEventPointedTriangle()
        {



            if (EditorInputManager.GetMouseButton(0))
                EditedMesh.Dirty |= PointedTris.SetSharpCorners(SetTo);

            return false;
        }

        public void AutoAssignDominantNormalsForBeveling()
        {

            foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                vr.SmoothNormal = true;

            foreach (var t in EditedMesh.triangles) t.SetSharpCorners(true);

            foreach (var t in EditedMesh.triangles)
            {
                Vector3[] v3s = new Vector3[3];

                for (int i = 0; i < 3; i++)
                    v3s[i] = t.vertexes[i].Pos;

                float[] dist = new float[3];

                for (int i = 0; i < 3; i++)
                    dist[i] = (v3s[(i + 1) % 3] - v3s[(i + 2) % 3]).magnitude;

                for (int i = 0; i < 3; i++)
                {
                    var a = (i + 1) % 3;
                    var b = (i + 2) % 3;
                    if (dist[i] < dist[a] / Cfg.bevelDetectionSensetivity && dist[i] < dist[b] / Cfg.bevelDetectionSensetivity)
                    {
                        t.SetSharpCorners(false);

                        var other = (new LineData(t, t.vertexes[a], t.vertexes[b])).GetOtherTriangle();
                        if (other != null)
                            other.SetSharpCorners(false);
                    }
                }
            }

            EditedMesh.Dirty = true;
        }

        public override void KeysEventPointedTriangle()
        {

            if (KeyCode.N.IsDown())
            {

                if (!EditorInputManager.Alt)
                {
                    int no = PointedTris.NumberOf(PointedTris.GetClosestTo(MeshMGMT.collisionPosLocal));
                    PointedTris.DominantCourner[no] = !PointedTris.DominantCourner[no];
#if PEGI
                    (PointedTris.DominantCourner[no] ? "Triangle edge's Normal is now dominant" : "Triangle edge Normal is NO longer dominant").TeachingNotification();
#endif
                }
                else
                {
                    PointedTris.InvertNormal();
#if PEGI
                    "Inverting Normals".TeachingNotification();
#endif
                }

                MeshMGMT.editedMesh.Dirty = true;

            }


        }

        public override void KeysEventPointedLine()
        {
            if (KeyCode.N.IsDown())
            {
                foreach (var t in MeshMGMT.PointedLine.GetAllTriangles_USES_Tris_Listing())
                    t.SetSharpCorners(!EditorInputManager.Alt);
#if PEGI
                "N ON A LINE - Make triangle normals Dominant".TeachingNotification();
#endif
                MeshMGMT.editedMesh.Dirty = true;
            }
        }

        public override void KeysEventPointedVertex()
        {



            if (KeyCode.N.IsDown())
            {
                var m = MeshMGMT;

                m.PointedUV.meshPoint.SmoothNormal = !m.PointedUV.meshPoint.SmoothNormal;
                m.editedMesh.Dirty = true;
#if PEGI
                "N - on Vertex - smooth Normal".TeachingNotification();
#endif
            }

        }

    }

    public class SmoothingTool : MeshToolBase
    {

        public bool MergeUnmerge = false;

        public static SmoothingTool inst;
        
        public SmoothingTool()
        {
            inst = this;
        }

        public override bool ShowVerticesDefault => true;

        public override bool ShowLines => true;

        #region Inspector
        public override string Tooltip => "Click to set vertex as smooth/sharp" + Environment.NewLine;

        public override string NameForPEGIdisplay => "Vertex Smoothing";

#if PEGI
        public override bool Inspect()
        {

            MeshManager m = MeshMGMT;

            pegi.write("OnClick:", 60);
            if ((MergeUnmerge ? "Merging (Shift: Unmerge)" : "Smoothing (Shift: Unsmoothing)").Click().nl())
                MergeUnmerge = !MergeUnmerge;

            if ("Sharp All".Click())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = false;
                m.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = false;
            }

            if ("Smooth All".Click().nl())
            {
                foreach (MeshPoint vr in MeshMGMT.editedMesh.meshPoints)
                    vr.SmoothNormal = true;
                m.editedMesh.Dirty = true;
                Cfg.newVerticesSmooth = true;
            }


            if ("All shared".Click())
            {
                m.editedMesh.AllVerticesShared();
                m.editedMesh.Dirty = true;
                Cfg.newVerticesUnique = false;
            }

            if ("All unique".Click().nl())
            {
                foreach (Triangle t in EditedMesh.triangles)
                    m.editedMesh.Dirty |= m.editedMesh.GiveTriangleUniqueVerticles(t);
                Cfg.newVerticesUnique = true;
            }



            return false;

        }
#endif
        #endregion

        public override bool MouseEventPointedTriangle()
        {

            if (EditorInputManager.GetMouseButton(0))
            {
                if (MergeUnmerge)
                {
                    if (EditorInputManager.Shift)
                        EditedMesh.Dirty |= PointedTris.SetAllVerticesShared();
                    else
                        EditedMesh.Dirty |= EditedMesh.GiveTriangleUniqueVerticles(PointedTris);
                }
                else
                    EditedMesh.Dirty |= PointedTris.SetSmoothVertices(!EditorInputManager.Shift);
            }

            return false;
        }

        public override bool MouseEventPointedVertex()
        {
            if (EditorInputManager.GetMouseButton(0))
            {
                if (MergeUnmerge)
                {
                    if (EditorInputManager.Shift)
                        EditedMesh.Dirty |= PointedVertex.SetAllUVsShared(); // .SetAllVerticesShared();
                    else
                        EditedMesh.Dirty |= PointedVertex.AllPointsUnique(); //editedMesh.GiveTriangleUniqueVerticles(pointedTris);
                }
                else
                    EditedMesh.Dirty |= PointedVertex.SetSmoothNormal(!EditorInputManager.Shift);
            }

            return false;
        }

        public override bool MouseEventPointedLine()
        {
            if (EditorInputManager.GetMouseButton(0))
            {
                if (MergeUnmerge)
                {
                    if (!EditorInputManager.Shift)
                        Dirty |= PointedLine.AllVerticesShared();
                    else
                    {
                        Dirty |= PointedLine.GiveUniqueVerticesToTriangles();

                    }
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                        Dirty |= PointedLine[i].SetSmoothNormal(!EditorInputManager.Shift);
                }
            }

            return false;
        }

    }

    /*
    public class VertexAnimationTool : MeshToolBase
    {
        public override string ToString() { return "vertex Animation"; }

        public override void AssignText(MarkerWithText mrkr, vertexpointDta vpoint)
        {

          
                mrkr.textm.text = vpoint.index.ToString();
             
        }

          public void TextureAnim_ToCollider() {
            _PreviewMeshGen.CopyFrom(_Mesh);
            _PreviewMeshGen.AddTextureAnimDisplacement();
            MeshConstructor con = new MeshConstructor(_Mesh, target.meshProfile, null);
            con.AssignMeshAsCollider(target.meshCollider );
        }

        public override void MouseEventPointedVertex()
        {
          
            if (meshMGMT.pointedUV == null) return;
            if (EditorInputManager.GetMouseButtonDown(1))
            {
                meshMGMT.draggingSelected = true;
                meshMGMT.dragDelay = 0.2f;
            }
        }

        public override bool showGrid { get { return true; } }

        public override void ManageDragging()
        {
            if ((EditorInputManager.GetMouseButtonUp(1)) || (EditorInputManager.GetMouseButton(1) == false))
            {
                meshMGMT.draggingSelected = false;
                mesh.dirty = true;

            }
            else
            {
                meshMGMT.dragDelay -= Time.deltaTime;
                if ((meshMGMT.dragDelay < 0) || (Application.isPlaying == false))
                {
                    if (meshMGMT.selectedUV == null) { meshMGMT.draggingSelected = false; Debug.Log("no selected"); return; }
                    if ((GridNavigator.inst().angGridToCamera(GridNavigator.onGridPos) < 82) &&
                               meshMGMT.target.AnimatedVertices())
                                    meshMGMT.selectedUV.vert.AnimateTo(meshMGMT.onGridLocal);
                                
                    
                        
                    
                }
            }
        }

    }
    */

    public class VertexColorTool : MeshToolBase
    {
        public static VertexColorTool inst;

        // public override MeshTool myTool { get { return MeshTool.VertColor; } }

        public override Color VertColor => Color.white;

        #region Inspector
        public override string Tooltip => " 1234 on Line - apply RGBA for Border.";

        public override string NameForPEGIdisplay => "vertex Color";

#if PEGI
        public override bool Inspect()
        {
            bool changed = false;

            if (("Paint All with Brush Color").Click().nl(ref changed))
                MeshMGMT.editedMesh.PaintAll(Cfg.brushConfig.colorLinear);

            "Make Vertices Unique On coloring".toggle(60, ref Cfg.MakeVericesUniqueOnEdgeColoring).nl(ref changed);

            var br = Cfg.brushConfig;

            var col = br.Color;

            if (pegi.edit(ref col).nl(ref changed))
                br.Color = col;

                
            br.ColorSliders().nl();
            
            return changed;
        }
#endif
        #endregion

        public override bool MouseEventPointedVertex()
        {
            MeshManager m = MeshMGMT;

            BrushConfig bcf = GlobalBrush;

            //if (EditorInputManager.GetMouseButtonDown(1))
            //  m.pointedUV.vert.clearColor(cfg.brushConfig.mask);

            if ((EditorInputManager.GetMouseButtonDown(0)))
            {
                if (EditorInputManager.Control)

                    bcf.mask.Transfer(ref m.PointedUV._color, bcf.Color);

                else
                    foreach (Vertex uvi in m.PointedUV.meshPoint.uvpoints)
                        bcf.mask.Transfer(ref uvi._color, Cfg.brushConfig.Color);

                m.editedMesh.dirty_Color = true;
            }

            return false;
        }

        public override bool MouseEventPointedLine()
        {
            if (EditorInputManager.GetMouseButton(0))
            {
                if (PointedLine.SameAsLastFrame)
                    return true;

                BrushConfig bcf = Cfg.brushConfig;

                Vertex a = PointedLine.pnts[0];
                Vertex b = PointedLine.pnts[1];

                Color c = bcf.Color;

                a.meshPoint.SetColorOnLine(c, bcf.mask, b.meshPoint);//setColor(glob.colorSampler.color, glob.colorSampler.mask);
                b.meshPoint.SetColorOnLine(c, bcf.mask, a.meshPoint);
                MeshMGMT.editedMesh.dirty_Color = true;
                return true;
            }
            return false;
        }

        public override bool MouseEventPointedTriangle()
        {
            if (EditorInputManager.GetMouseButton(0))
            {

                if (PointedTris.SameAsLastFrame)
                    return true;

                BrushConfig bcf = Cfg.brushConfig;

                Color c = bcf.Color;

                foreach (var u in PointedTris.vertexes)
                    foreach (var vuv in u.meshPoint.uvpoints)
                        bcf.mask.Transfer(ref vuv._color, c);

                //  a.vert.SetColorOnLine(c, bcf.mask, b.vert);//setColor(glob.colorSampler.color, glob.colorSampler.mask);
                // b.vert.SetColorOnLine(c, bcf.mask, a.vert);
                MeshMGMT.editedMesh.dirty_Color = true;
                return true;
            }
            return false;
        }

        public override void KeysEventPointedLine()
        {
            Vertex a = PointedLine.pnts[0];
            Vertex b = PointedLine.pnts[1];

            int ind = Event.current.NumericKeyDown();

            if ((ind > 0) && (ind < 5))
            {
                a.meshPoint.FlipChanelOnLine((ColorChanel)(ind - 1), b.meshPoint);
                MeshMGMT.editedMesh.Dirty = true;
                Event.current.Use();
            }

        }

        public VertexColorTool()
        {
            inst = this;
        }

    }

    public class VertexEdgeTool : MeshToolBase {

        static bool AlsoDoColor = false;
        static bool editingFlexibleEdge = false;
        public static float edgeValue = 1;

        public static float ShiftInvertedVelue => EditorInputManager.Shift ? 1f - edgeValue : edgeValue;
        
        public override bool ShowTriangles => false;

        public override bool ShowVerticesDefault => !editingFlexibleEdge;

        #region Inspector
        
        public override string NameForPEGIdisplay => "vertex Edge";
        
        public override string Tooltip =>
             "Shift - invert edge value" + Environment.NewLine +
             "This tool allows editing value if edge.w" + Environment.NewLine + "edge.xyz is used to store border information about the triendle. ANd the edge.w in Example shaders is used" +
             "to mask texture color with vertex color. All vertices should be set as Unique for this to work." +
             "Line is drawn between vertices marked with line strength 1. A triangle can't have only 2 sides with Edge: it's eather 1 side, or all 3 (2 points marked to create a line, or 3 points to create 3 lines).";


#if PEGI
        public static pegi.CallDelegate PEGIdelegates;

        public override bool Inspect()
        {
            bool changed = false;
            "Edge Strength: ".edit(ref edgeValue).nl(ref changed);
            "Also do color".toggle(ref AlsoDoColor).nl(ref changed);

            if (AlsoDoColor)
                changed |= GlobalBrush.ColorSliders().nl();

            if (EditedMesh.submeshCount > 1 && "Apply To Lines between submeshes".Click().nl())
                LinesBetweenSubmeshes();

            if (PEGIdelegates != null)
                foreach (pegi.CallDelegate d in PEGIdelegates.GetInvocationList())
                    changed |= d();

            "Flexible Edge".toggleIcon("Edge type can be seen in Packaging profile (if any). Only Bevel shader doesn't have a Flexible edge.", ref editingFlexibleEdge);

            return changed;
        }
#endif
        #endregion

        void LinesBetweenSubmeshes()
        {

        }

        public override bool MouseEventPointedVertex()
        {


            if ((EditorInputManager.GetMouseButton(0)))
            {
#if PEGI
                if (!PointedUV.meshPoint.AllPointsUnique())
                    "Shared points found, Edge requires All Unique".showNotificationIn3D_Views();
#endif
                if (EditorInputManager.Control)
                {
                    edgeValue = MeshMGMT.PointedUV.meshPoint.edgeStrength;
                    if (AlsoDoColor) GlobalBrush.Color = PointedUV._color;

                    // foreach (UVpoint uvi in m.pointedUV.vert.uvpoints)
                    //   bcf.mask.Transfer(ref uvi._color, cfg.brushConfig.colorLinear.ToGamma());
                }
                else
                {

                    if (PointedUV.SameAsLastFrame)
                        return true;

                    MeshMGMT.PointedUV.meshPoint.edgeStrength = ShiftInvertedVelue;
                    if (AlsoDoColor)
                    {
                        var col = GlobalBrush.Color;
                        foreach (Vertex uvi in PointedUV.meshPoint.uvpoints)
                            GlobalBrush.mask.Transfer(ref uvi._color, col);
                    }
                    MeshMGMT.editedMesh.Dirty = true;

                    return true;
                }



            }
            return false;
        }

        public override bool MouseEventPointedLine()
        {
            if (EditorInputManager.GetMouseButton(0))
            {

                var vrtA = PointedLine.pnts[0].meshPoint;
                var vrtB = PointedLine.pnts[1].meshPoint;

                if (EditorInputManager.Control)
                    edgeValue = (vrtA.edgeStrength + vrtB.edgeStrength) * 0.5f;
                else
                {
                    if (PointedLine.SameAsLastFrame)
                        return true;


                    PutEdgeOnLine(PointedLine);

                    return true;
                    /* vrtA.edgeStrength = edgeValue;
                     vrtB.edgeStrength = edgeValue;

                     var tris = pointedLine.getAllTriangles_USES_Tris_Listing();

                     foreach (var t in tris)
                         t.edgeWeight[t.NotOnLineIndex(pointedLine)] = edgeValue;// true;


                     if (AlsoDoColor) {
                         var col = globalBrush.colorLinear.ToGamma();
                         foreach (UVpoint uvi in vrtA.uvpoints)
                             globalBrush.mask.Transfer(ref uvi._color, col);
                         foreach (UVpoint uvi in vrtB.uvpoints)
                             globalBrush.mask.Transfer(ref uvi._color, col);
                     }

                     meshMGMT.edMesh.dirty = true;*/
                }
            }
            return false;
        }

        public static void PutEdgeOnLine(LineData ld)
        {

            var vrtA = ld.pnts[0].meshPoint;
            var vrtB = ld.pnts[1].meshPoint;

            var tris = ld.GetAllTriangles_USES_Tris_Listing();

            foreach (var t in tris)
                t.edgeWeight[t.GetIndexOfNoOneIn(ld)] = ShiftInvertedVelue;// true;

            float edValA = ShiftInvertedVelue;
            float edValB = ShiftInvertedVelue;

            if (editingFlexibleEdge)
            {

                foreach (var uv in vrtA.uvpoints)
                    foreach (var t in uv.tris)
                    {
                        var opposite = t.NumberOf(uv);
                        for (int i = 0; i < 3; i++)
                            if (opposite != i)
                                edValA = Mathf.Max(edValA, t.edgeWeight[i]);
                    }

                foreach (var uv in vrtB.uvpoints)
                    foreach (var t in uv.tris)
                    {
                        var opposite = t.NumberOf(uv);
                        for (int i = 0; i < 3; i++)
                            if (opposite != i)
                                edValB = Mathf.Max(edValB, t.edgeWeight[i]);
                    }
            }

            vrtA.edgeStrength = edValA;
            vrtB.edgeStrength = edValB;

            if (AlsoDoColor)
            {
                var col = GlobalBrush.Color;
                foreach (Vertex uvi in vrtA.uvpoints)
                    GlobalBrush.mask.Transfer(ref uvi._color, col);
                foreach (Vertex uvi in vrtB.uvpoints)
                    GlobalBrush.mask.Transfer(ref uvi._color, col);
            }

            MeshMGMT.editedMesh.Dirty = true;

        }

        #region Encode & Decode
        public override bool Decode(string tag, string data)
        {
            switch (tag)
            {
                case "v": edgeValue = data.ToFloat(); break;
                case "doCol": AlsoDoColor = data.ToBool(); break;
                case "fe": editingFlexibleEdge = data.ToBool(); break;
                default: return false;
            }
            return true;
        }

        public override StdEncoder Encode() => new StdEncoder()
            .Add("v", edgeValue)
            .Add_Bool("doCol", AlsoDoColor)
            .Add_Bool("fe", editingFlexibleEdge);
        #endregion

    }

    public class TriangleSubmeshTool : MeshToolBase
    {
        public int curSubmesh = 0;
        
        public override bool ShowVerticesDefault => false;

        public override bool ShowLines => false;
        
        public override bool MouseEventPointedTriangle()
        {

            if (EditorInputManager.GetMouseButtonDown(0) && EditorInputManager.Control)
            {
                curSubmesh = (int)MeshMGMT.PointedTris.submeshIndex;
#if PEGI
                ("Submesh " + curSubmesh).showNotificationIn3D_Views();
#endif
            }

            if (EditorInputManager.GetMouseButton(0) && !EditorInputManager.Control && (MeshMGMT.PointedTris.submeshIndex != curSubmesh))
            {
                if (PointedTris.SameAsLastFrame)
                    return true;
                MeshMGMT.PointedTris.submeshIndex = curSubmesh;
                EditedMesh.submeshCount = Mathf.Max(MeshMGMT.PointedTris.submeshIndex + 1, EditedMesh.submeshCount);
                EditedMesh.Dirty = true;
                return true;
            }
            return false;
        }

        #region Inspector

        public override string Tooltip => "Ctrl+LMB - sample" + Environment.NewLine + "LMB on triangle - set submesh";
        public override string NameForPEGIdisplay => "triangle Submesh index";


#if PEGI
        public override bool Inspect()
        {
            ("Total Submeshes: " + EditedMesh.submeshCount).nl();

            "Submesh: ".edit(60, ref curSubmesh).nl();
            return false;
        }
#endif
        #endregion

        #region Encode & Decode
        public override StdEncoder Encode()
        {
            var cody = new StdEncoder();
            if (curSubmesh != 0)
                cody.Add("sm", curSubmesh);
            return cody;
        }

        public override bool Decode(string tag, string data)
        {
            switch (tag)
            {
                case "sm": curSubmesh = data.ToInt(); break;
                default: return false;
            }
            return true;

        }
        #endregion
    }

    public class VertexGroupTool : MeshToolBase
    {
        public override string NameForPEGIdisplay => "Vertex Group";
    }

}