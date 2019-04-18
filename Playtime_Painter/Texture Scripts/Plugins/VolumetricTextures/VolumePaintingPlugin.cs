﻿
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using UnityEngine;
using PlayerAndEditorGUI;
using QuizCannersUtilities;

namespace Playtime_Painter
{

    [TaggedType(tag)]
    public class VolumePaintingPlugin : PainterSystemManagerPluginBase, IGotDisplayName,
        IPainterManagerPluginComponentPEGI, IPainterManagerPluginBrush, IPainterManagerPluginGizmis, IUseDepthProjector, IUseReplacementCamera {

        const string tag = "VolumePntng";
        public override string ClassTag => tag;

        public static ShaderProperty.VectorValue VOLUME_H_SLICES = new ShaderProperty.VectorValue("VOLUME_H_SLICES");
        public static ShaderProperty.VectorValue VOLUME_POSITION_N_SIZE = new ShaderProperty.VectorValue("VOLUME_POSITION_N_SIZE");

        public static ShaderProperty.VectorValue VOLUME_H_SLICES_Global = new ShaderProperty.VectorValue( PainterDataAndConfig.GlobalPropertyPrefix + "VOLUME_H_SLICES");
        public static ShaderProperty.VectorValue VOLUME_POSITION_N_SIZE_Global = new ShaderProperty.VectorValue(PainterDataAndConfig.GlobalPropertyPrefix + "VOLUME_POSITION_N_SIZE");
        
        public static ShaderProperty.VectorValue VOLUME_H_SLICES_BRUSH = new ShaderProperty.VectorValue("VOLUME_H_SLICES_BRUSH");
        public static ShaderProperty.VectorValue VOLUME_POSITION_N_SIZE_BRUSH = new ShaderProperty.VectorValue("VOLUME_POSITION_N_SIZE_BRUSH");

        public const string VolumeTextureTag = "_VOL";
        public const string VolumeSlicesCountTag = "_slices";

        private bool _useGrid;
        
        private static Shader _preview;
        private static Shader _brush;
        private static Shader _rayTraceBrush;

        #region Encode & Decode

        public override CfgEncoder Encode()
        {
            var cody = this.EncodeUnrecognized()
                .Add_IfTrue("ug", _useGrid)
                .Add_IfTrue("rtr", _enableRayTracing)
                .Add("cam", rayTraceCameraConfiguration);

            return cody;
        }

        public override bool Decode(string tg, string data)
        {
            switch (tg)
            {
                case "ug": _useGrid = data.ToBool(); break;
                case "rtr": _enableRayTracing = true; break;
                case "cam": rayTraceCameraConfiguration.Decode(data); break;
                default: return false;
            }
            return true;
        }

        #endregion

        public static VolumePaintingPlugin _inst;

        public override void Enable()
        {
            base.Enable();
            _inst = this;

            if (!_preview)
                _preview = Shader.Find("Playtime Painter/Editor/Preview/Volume");

            if (!_brush)
                _brush = Shader.Find("Playtime Painter/Editor/Brush/Volume");          

            if (!_rayTraceBrush)
                _rayTraceBrush = Shader.Find("Playtime Painter/Editor/Brush/Volume_RayTrace");
        }
        
        public Shader GetPreviewShader(PlaytimePainter p) => p.GetVolumeTexture() ? _preview : null;
        
        public Shader GetBrushShaderDoubleBuffer(PlaytimePainter p) => 
            p.GetVolumeTexture() ? (_enableRayTracing ? _rayTraceBrush : _brush) : null;

        public Shader GetBrushShaderSingleBuffer(PlaytimePainter p) => null;

        public bool NeedsGrid(PlaytimePainter p) => _useGrid && p.GetVolumeTexture();
        
        public bool IsA3DBrush(PlaytimePainter painter, BrushConfig bc, ref bool overrideOther)
        {
            if (!painter.GetVolumeTexture()) return false;
            overrideOther = true;
            return true;
        }

        public bool PaintPixelsInRam(StrokeVector stroke, float brushAlpha, ImageMeta image, BrushConfig bc, PlaytimePainter painter) {

            var volume = image.texture2D.GetVolumeTextureData();

            if (!volume) return false;
            
            if (volume.VolumeJobIsRunning)
                return false;

            bc.brush3DRadius = Mathf.Min(BrushScaleMaxForCpu(volume), bc.brush3DRadius);

            var volumeScale = volume.size;
            
            var pos = (stroke.posFrom - volume.transform.position) / volumeScale + 0.5f * Vector3.one;

            var height = volume.Height;
            var texWidth = image.width;

            BlitFunctions.brAlpha = brushAlpha;
            bc.PrepareCpuBlit(image);
            BlitFunctions.half = bc.Size(true) / volumeScale;

            var pixels = image.Pixels;

            var iHalf = (int)(BlitFunctions.half - 0.5f);
            var smooth = bc.GetBrushType(true) != BrushTypePixel.Inst;
            if (smooth) iHalf += 1;

            BlitFunctions.alphaMode = BlitFunctions.SphereAlpha;

            var sliceWidth = texWidth / volume.hSlices;

            var hw = sliceWidth / 2;

            var y = (int)pos.y;
            var z = (int)(pos.z + hw);
            var x = (int)(pos.x + hw);

            for (BlitFunctions.y = -iHalf; BlitFunctions.y < iHalf + 1; BlitFunctions.y++)
            {

                var h = y + BlitFunctions.y;

                if (h >= height) return true;

                if (h < 0) continue;
                var hy = h / volume.hSlices;
                var hx = h % volume.hSlices;
                var hTexIndex = (hy * texWidth + hx) * sliceWidth;

                for (BlitFunctions.z = -iHalf; BlitFunctions.z < iHalf + 1; BlitFunctions.z++)
                {

                    var trueZ = z + BlitFunctions.z;

                    if (trueZ < 0 || trueZ >= sliceWidth) continue;
                    var yTexIndex = hTexIndex + trueZ * texWidth;

                    for (BlitFunctions.x = -iHalf; BlitFunctions.x < iHalf + 1; BlitFunctions.x++)
                    {
                        if (!BlitFunctions.alphaMode()) continue;
                        var trueX = x + BlitFunctions.x;

                        if (trueX < 0 || trueX >= sliceWidth) continue;
                        var texIndex = yTexIndex + trueX;
                        BlitFunctions.blitMode(ref pixels[texIndex]);
                    }
                }
            }

            return true;
        }

        public bool PaintRenderTexture(StrokeVector stroke, ImageMeta image, BrushConfig bc, PlaytimePainter painter)
        {
            var vt = painter.GetVolumeTexture();
            if (!vt)
                return false;

            if (_enableRayTracing) {

                rayTraceCameraConfiguration.From(stroke);

                delayedPaintingConfiguration = new BrushStrokePainterImage(stroke, image, bc, painter);

                PainterCamera.GetProjectorCamera().RenderRightNow(this);
            }
            else
                PaintRenderTexture(new BrushStrokePainterImage(stroke, image, bc, painter));



            /*
                BrushTypeSphere.Inst.BeforeStroke(painter, bc, stroke);
               VOLUME_POSITION_N_SIZE_BRUSH.GlobalValue = vt.PosSize4Shader;
               VOLUME_H_SLICES_BRUSH.GlobalValue = vt.Slices4Shader;
               if (stroke.mouseDwn)
                   stroke.posFrom = stroke.posTo;

               image.useTexCoord2 = false;
               bool alphaBuffer;
               TexMGMT.Shader_UpdateStrokeSegment(bc, bc.Speed * 0.05f, image, stroke, painter, out alphaBuffer);
               stroke.SetWorldPosInShader();

               TexMGMT.brushRenderer.FullScreenQuad();

               TexMGMT.Render();

               BrushTypeSphere.Inst.AfterStroke_Painter(painter, bc, stroke, alphaBuffer, image);*/

            return true;
        }

        public bool PaintRenderTexture(BrushStrokePainterImage cfg)
        {
            var stroke = cfg.stroke;
            var image = cfg.image;
            var painter = cfg.painter;
            var bc = cfg.brush;

            var vt = painter.GetVolumeTexture();
            stroke.posFrom = stroke.posTo;

            BrushTypeSphere.Inst.BeforeStroke(painter, bc, stroke);

            VOLUME_POSITION_N_SIZE_BRUSH.GlobalValue = vt.PosSize4Shader;
            VOLUME_H_SLICES_BRUSH.GlobalValue = vt.Slices4Shader;
            //if (stroke.mouseDwn)
               

            image.useTexCoord2 = false;
            bool alphaBuffer;
            TexMGMT.Shader_UpdateStrokeSegment(bc, bc.Speed * 0.05f, image, stroke, painter, out alphaBuffer);
            stroke.SetWorldPosInShader();

            TexMGMT.brushRenderer.FullScreenQuad();

            TexMGMT.Render();

            BrushTypeSphere.Inst.AfterStroke_Painter(painter, bc, stroke, alphaBuffer, image);

            return true;
        }
        
        #region Ray Tracing

        private bool _enableRayTracing;

        const int targetScale = 8;

        private static Texture2D getMinSizeTexture()
        {
            if (tex)
                return tex;

            tex = new Texture2D(targetScale, targetScale, TextureFormat.RGBA32, false, true);

            return tex;
        }

        private static Texture2D tex;

        private BrushStrokePainterImage delayedPaintingConfiguration;

        private static ProjectorCameraConfiguration rayTraceCameraConfiguration = new ProjectorCameraConfiguration();
        
        public bool ProjectorReady() => delayedPaintingConfiguration!= null;

        public CameraMatrixParameters GetGlobalCameraMatrixParameters() => null;

        public ProjectorCameraConfiguration GetProjectorCameraConfiguration() => rayTraceCameraConfiguration;

        public void AfterCameraRender(RenderTexture texture)
        {

            const int pixelsCount = targetScale * targetScale;

            var tiny = TexMGMT.GetDownscaleOf(texture, targetScale);

            var pix = getMinSizeTexture().CopyFrom(tiny).GetPixels();

            Color avg = Color.black;
            //float totalConfidance = 0;

            foreach (var p in pix)
            {
                //float alpha = p.a;
                avg += p;// * alpha;
                //totalConfidance += alpha;
            }

            GlobalBrush.Color = avg / (float)pixelsCount;

            PainterCamera.BrushColorProperty.GlobalValue = GlobalBrush.Color;

            //Debug.Log("Returning Ray Trace for {0} : {1} ".F(rayTraceCameraConfiguration.position, pix[0].ToString()));

            PaintRenderTexture(delayedPaintingConfiguration);
            delayedPaintingConfiguration = null;
        }
        
        public RenderTexture GetTargetTexture() => RenderTextureBuffersManager.GetRenderTextureWithDepth();

        public ProjectorMode GetMode() => ProjectorMode.ReplacementShader;

        public string ProjectorTagToReplace => "RenderType";

        public Shader ProjectorShaderToReplaceWith => TexMGMTdata.rayTraceOutput;


        #endregion

        #region Inspector
        float BrushScaleMaxForCpu(VolumeTexture volTex) => volTex.size * volTex.Width * 0.025f;
        
#if PEGI
        public override string NameForDisplayPEGI => "Volume Painting";

        public bool ComponentInspector()
        {
            var id = InspectedPainter.ImgMeta;
            
            if (id != null) return false;
            
            var inspectedProperty = InspectedPainter.GetMaterialTextureProperty;
            
            if (inspectedProperty == null || !inspectedProperty.NameForDisplayPEGI.Contains(VolumeTextureTag)) return false;

            if (inspectedProperty.IsGlobalVolume()) {
                "Global Volume Expected".nl();

                "Create a game object with one of the Volume scripts and set it as Global Parameter"
                    .fullWindowDocumentationClick("About Global Volume Parameters");

            }
            else
            {
                "Volume Texture Expected".nl();

                var tmp = -1;

                if (!"Available:".select_Index(60, ref tmp, VolumeTexture.all))
                {
                    var vol = VolumeTexture.all.TryGet(tmp);

                    if (!vol) return false;

                    if (vol.ImageMeta != null)
                        vol.AddIfNew(InspectedPainter);
                    else
                        "Volume Has No Texture".showNotificationIn3D_Views();

                    return true;
                }
            }

            return false;
        }

        private bool _exploreVolumeData;

        private bool _exploreRayTaceCamera;

   
        public bool BrushConfigPEGI(ref bool overrideBlitMode, BrushConfig br) {

            var changed = false;

            var p = InspectedPainter;

            var volTex = p.GetVolumeTexture();

            if (volTex) {

                overrideBlitMode = true;

                var id = p.ImgMeta;

                if (BrushConfig.showAdvanced) 
                    "Grid".toggle(50, ref _useGrid).nl();
                
                if (BrushConfig.showAdvanced || _enableRayTracing)
                    "Ray-Tracing".toggleIcon(ref _enableRayTracing).nl();



                if (_enableRayTracing)
                {
                    var dp = PainterCamera.depthProjectorCamera;
                    if (dp && dp.pauseAutoUpdates)
                        "Light Projectors paused".toggleIcon(ref dp.pauseAutoUpdates).nl(ref changed);
                    
                }

                if ("Ray Trace Camera".conditional_enter(_enableRayTracing, ref _exploreRayTaceCamera).nl())
                    rayTraceCameraConfiguration.Nested_Inspect().changes(ref changed);

                if ((volTex.name + " " + id.texture2D.VolumeSize(volTex.hSlices)).foldout(ref _exploreVolumeData).nl())
                    changed |= volTex.Nested_Inspect();

                if (volTex.NeedsToManageMaterials)
                {
                    var painterMaterial = InspectedPainter.Material;
                    if (painterMaterial != null) {
                        if (!volTex.materials.Contains(painterMaterial))
                            if ("Add This Material".Click().nl())
                                volTex.AddIfNew(p);
                    }
                }

                var cpuBlit = id.TargetIsTexture2D().nl();
                
                if (cpuBlit)
                    "Painting volume with CPU brush is very slow".writeWarning();
                else
                {
                    if (!br.GetBrushType(false).IsAWorldSpaceBrush) {
                        "Only World space brush can edit volumes".writeHint();
                        pegi.nl();
                        if ("Change to Sphere brush".Click())
                            br.SetBrushType(false, BrushTypeSphere.Inst);
                    }
                }

                pegi.nl();

                if (!cpuBlit)
                   "Hardness:".edit("Makes edges more rough.", 70, ref br.hardness, 1f, 22f).nl(ref changed);

                var tmpSpeed = br._dSpeed.value;
                if ("Speed".edit(40, ref tmpSpeed, 0.01f, 4.5f).nl(ref changed))
                    br._dSpeed.value = tmpSpeed;


              
                var maxScale =  volTex.size * volTex.Width * 4;

                "Scale:".edit(40, ref br.brush3DRadius, 0.001f * maxScale, maxScale * 0.5f).changes(ref changed);

                if (cpuBlit && br.brush3DRadius > BrushScaleMaxForCpu(volTex))
                    icon.Warning.write("Size will be reduced when panting due to low performance of the CPU brush for volumes");

                pegi.nl();

                /*
                if (br.GetBlitMode(cpuBlit).UsingSourceTexture && id.TargetIsRenderTexture())
                {
                    if (TexMGMTdata.sourceTextures.Count > 0)
                    {
                        "Copy From:".write(70);
                        changed |= pegi.selectOrAdd(ref br.selectedSourceTexture, ref TexMGMTdata.sourceTextures);
                    }
                    else
                        "Add Textures to Render Camera to copy from".nl();
                }*/

            }
            if (changed) this.SetToDirty_Obj();

            return changed;
        }

        private int _exploredVolume;
        public override bool Inspect()
        {
            var changes = false;

            "Volumes".edit_List(ref VolumeTexture.all, ref _exploredVolume).changes(ref changes);

            return changes;
        }
#endif

        public bool PlugIn_PainterGizmos(PlaytimePainter painter)
        {
            var volume = painter.ImgMeta.GetVolumeTextureData();

            if (volume && !painter.LockTextureEditing)
                return volume.DrawGizmosOnPainter(painter);

            return false;
        }
        #endregion

    }


    [TaggedType(Tag)]
    public class VolumeTextureManagement : PainterComponentPluginBase
    {
        private const string Tag = "VolTexM";
        public override string ClassTag => Tag;

        public override void GetNonMaterialTextureNames(PlaytimePainter painter, ref List<ShaderProperty.TextureValue> dest)
        {
            var mat = painter.Material;

            if (!mat) 
                return;
                    
            var tg = mat.GetTag("Volume", false, "");

            if (!tg.IsNullOrEmpty()) {

                foreach (var v in VolumeTexture.all)
                {

                    var mp = v.MaterialPropertyNameGlobal;

                    if (mp.NameForDisplayPEGI.Equals(tg))
                    {

                        dest.Add(mp);
                        return;
                    }
                }
            }

        }

        public override bool SetTextureOnMaterial(ShaderProperty.TextureValue field, ImageMeta id, PlaytimePainter painter)
        {
            if (!field.IsGlobalVolume()) return false;

            var gl = VolumeTexture.GetGlobal(field);

            if (gl != null)
            {
                gl.ImageMeta = id;
                gl.UpdateMaterials();
            }

            return true;
        }

        public override bool GetTexture(ShaderProperty.TextureValue field, ref Texture tex, PlaytimePainter painter)
        {
            if (!field.IsGlobalVolume()) return false;

            var gl = VolumeTexture.GetGlobal(field);

            if (gl != null)
                tex = gl.ImageMeta.CurrentTexture();
            else
                tex = null;

            return true;
        }

        public override bool UpdateTilingFromMaterial(ShaderProperty.TextureValue fieldName, PlaytimePainter painter)
        {
            if (!fieldName.IsGlobalVolume()) return false;
            var id = painter.ImgMeta;
            if (id == null) return true;
            id.tiling = Vector2.one;
            id.offset = Vector2.zero;
            return true;
        }
    }

    public static class VolumeEditingExtensions
    {

        public static bool IsGlobalVolume(this ShaderProperty.TextureValue field) {
            
            string name = field.NameForDisplayPEGI;
            if (name.Contains(PainterDataAndConfig.GlobalPropertyPrefix) &&
                name.Contains(VolumePaintingPlugin.VolumeTextureTag))
                return true;
            return false;
            
        }

        public static VolumeTexture GetVolumeTexture(this PlaytimePainter p)
        {
            if (!p)
                return null;

            var id = p.ImgMeta;

            if (id != null && id.texture2D)
                return id.GetVolumeTextureData();

            return null;
        }

        public static Vector3 VolumeSize(this Texture2D tex, int slices)
        {
            var w = tex.width / slices;
            return new Vector3(w, slices * slices, w);
        }

        public static void SetVolumeTexture(this IEnumerable<Material> materials, VolumeTexture vt) {
            if (!vt)
                return;
            var imd = vt.ImageMeta;

            if (imd == null)
                return;

            imd.isAVolumeTexture = true;

            var pnS = vt.PosSize4Shader;
            var vhS = vt.Slices4Shader;
            var tex = vt.MaterialPropertyName;
            
            foreach (var m in materials)
                if (m)
                {
                    VolumePaintingPlugin.VOLUME_POSITION_N_SIZE.SetOn(m, pnS);
                    VolumePaintingPlugin.VOLUME_H_SLICES.SetOn(m, vhS);
                    tex.SetOn(m, imd.CurrentTexture());
                }
        }

        public static VolumeTexture GetVolumeTextureData(this Texture tex) => GetVolumeTextureData(tex.GetImgData());
        
        private static VolumeTexture _lastFetchedVt;

        public static VolumeTexture GetVolumeTextureData(this ImageMeta id)
        {
            if (VolumePaintingPlugin._inst == null || id == null)
                return null;

            if (_lastFetchedVt != null && _lastFetchedVt.ImageMeta != null && _lastFetchedVt.ImageMeta == id)
                return _lastFetchedVt;

            for (var i = 0; i < VolumeTexture.all.Count; i++)
            {
                var vt = VolumeTexture.all[i];
                if (vt == null) { VolumeTexture.all.RemoveAt(i); i--; }

                else if (vt.ImageMeta != null && id == vt.ImageMeta)
                {
                    _lastFetchedVt = vt;
                    return vt;
                }
            }

            return null;
        }

    }
}