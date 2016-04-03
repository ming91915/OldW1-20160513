﻿Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports Autodesk.Revit
Imports OldW.GlobalSettings
Imports System.IO
Imports Autodesk.Revit.UI.Selection
Imports rvtTools_ez
Imports OldW.Soil

''' <summary>
''' 用来执行基坑开挖模拟中的数据存储与绘制操作
''' </summary>
''' <remarks></remarks>
Public Class ExcavationDoc
    Inherits OldWDocument

#Region "   ---   Fields"

    Private App As Autodesk.Revit.ApplicationServices.Application

#End Region

    Public Sub New(ByVal OldWDoc As OldWDocument)
        MyBase.New(OldWDoc.Document)
        Me.App = Document.Application
    End Sub

#Region "   ---   创建与设置土体"

#Region "   ---   创建模型土体与开挖土体"

    ''' <summary>
    ''' 创建模型土体，此土体单元在模型中应该只有一个。
    ''' </summary>
    ''' <param name="CreateMultipli">是否要在一个族中创建多个实体</param>
    ''' <param name="Depth">模型土体的深度，单位为m，数值为正表示向下的深度，反之表示向上的高度。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function CreateModelSoil(ByVal Depth As Double, Optional ByVal CreateMultipli As Boolean = False) As Soil_Model
        Dim SM As Soil_Model = Nothing

        ' 获得用来创建实体的模型线
        Dim CurveArrArr As CurveArrArray = GetLoopedCurveArray(CreateMultipli)
        If (Not CurveArrArr.IsEmpty) Then
            ' 构造一个族文档
            Dim FamilyDoc As Document = CreateSoilFamily()
            ExtrusionAndBindingDimension(FamilyDoc, CurveArrArr, Depth)

            ' 将族加载到项目文档中
            Dim f As Family = FamilyDoc.LoadFamily(Doc, UIDocument.GetRevitUIFamilyLoadOptions)
            FamilyDoc.Close(False)
            ' 获取一个族类型，以加载一个族实例到项目文档中
            Dim fs As FamilySymbol = f.GetFamilySymbolIds.First.Element(Doc)

            Using t As New Transaction(Doc, "添加族实例")
                t.Start()
                ' 为族重命名
                f.Name = Constants.FamilyName_Soil

                If Not fs.IsActive Then
                    fs.Activate()
                End If
                Dim fi As FamilyInstance = Doc.Create.NewFamilyInstance(New XYZ(), fs, [Structure].StructuralType.NonStructural)
                SM = Soil_Model.Create(Doc, fi)
                t.Commit()
            End Using
        End If
        Return SM
    End Function

    ''' <summary>
    ''' 创建开挖土体，此土体单元在模型中可以有很多个。
    ''' </summary>
    ''' <param name="CreateMultipli">在一个实体中是否可以有多个分隔的轮廓面</param>
    ''' <param name="Depth">模型土体的深度，单位为m，数值为正表示向下的深度，反之表示向上的高度。</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function CreateExcavationSoil(ByVal Depth As Double, ByVal CreateMultipli As Boolean, Optional ByVal ExcavatedDate As DateTime = Nothing) As Soil_Excav
        Dim SE As Soil_Excav = Nothing
        ' 获得用来创建实体的模型线
        Dim CurveArrArr As CurveArrArray = GetLoopedCurveArray(CreateMultipli)
        If (Not CurveArrArr.IsEmpty) Then
            ' 构造一个族文档
            Dim FamDoc As Document = CreateSoilFamily()
            ExtrusionAndBindingDimension(FamDoc, CurveArrArr, Depth)
            '
            Using t As New Transaction(FamDoc, "添加参数：开挖的完成日期")
                Try
                    t.Start()
                    ' 添加参数
                    Dim FM As FamilyManager = FamDoc.FamilyManager
                    Dim ExDef As ExternalDefinition = rvtTools.GetOldWDefinitionGroup(App).Definitions.Item(Constants.SP_ExcavationCompleted)
                    FM.AddParameter(ExDef, BuiltInParameterGroup.PG_DATA, True)
                    t.Commit()
                Catch ex As Exception
                    Dim res As DialogResult = MessageBox.Show("添加开挖的完成日期参数失败！" & vbCrLf & ex.Message, "出错", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    t.RollBack()
                End Try
            End Using

            ' 将族加载到项目文档中
            Dim fam As Family = FamDoc.LoadFamily(Doc, UIDocument.GetRevitUIFamilyLoadOptions)
            FamDoc.Close(False)
            ' 获取一个族类型，以加载一个族实例到项目文档中
            Dim fs As FamilySymbol = fam.GetFamilySymbolIds.First.Element(Doc)
            Using t As New Transaction(Doc, "添加族实例")
                t.Start()
                ' 族或族类型的重命名

                Dim DefaultDate As Date = "0001/01/01"

                Dim soilName As String = GetValidExcavationSoilName(Doc, DefaultDate)
                fam.Name = soilName
                fs.Name = soilName

                ' 创建实例
                If Not fs.IsActive Then
                    fs.Activate()
                End If
                Dim fi As FamilyInstance = Doc.Create.NewFamilyInstance(New XYZ(), fs, [Structure].StructuralType.NonStructural)
                SE = Soil_Excav.Create(fi, DefaultDate)
                t.Commit()
            End Using
        End If
        Return SE
    End Function

#End Region

    ''' <summary>
    ''' 创建一个模型土体（或者是开挖土体）的族文档，并将其打开。
    ''' </summary>
    Private Function CreateSoilFamily() As Document
        Dim app = Doc.Application
        Dim TemplateName As String = Path.Combine(ProjectPath.Path_family, Constants.FamilyTemplateName_Soil)
        If Not File.Exists(TemplateName) Then
            Throw New FileNotFoundException("在创建土体时，族样板文件没有找到", TemplateName)
        Else
            ' 创建族文档
            Dim FamDoc As Document = app.NewFamilyDocument(TemplateName)
            Using T As New Transaction(FamDoc, "设置族类别")
                T.Start()
                ' 设置族的族类别
                FamDoc.OwnerFamily.FamilyCategory = FamDoc.Settings.Categories.Item(BuiltInCategory.OST_Site)
                ' 设置族的名称
                T.Commit()
            End Using
            Return FamDoc
        End If
    End Function

    ' 几何创建
    ''' <summary> 从模型中获取要创建开挖土体的边界线 </summary>
    ''' <param name="CreateMultipli">在一个实体中是否可以有多个分隔的轮廓面</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function GetLoopedCurveArray(ByVal CreateMultipli As Boolean) As CurveArrArray
        Dim Profiles As New CurveArrArray  ' 每一次创建开挖土体时，在NewExtrusion方法中，要创建的实体的轮廓
        Dim blnStop As Boolean
        Do
            blnStop = True
            Dim cvLoop As CurveLoop = GetLoopCurve()
            ' 检验并添加
            If cvLoop.Count > 0 Then
                Dim cvArr As New CurveArray
                For Each c As Curve In cvLoop
                    cvArr.Append(c)
                Next
                Profiles.Append(cvArr)

                ' 是否要继续添加
                If CreateMultipli Then
                    Dim res As DialogResult = MessageBox.Show("曲线添加成功，是否还要继续添加？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                    If res = DialogResult.Yes Then
                        blnStop = False
                    End If
                End If
            End If
        Loop Until blnStop
        Return Profiles
    End Function
    ''' <summary>
    ''' 获取一组连续封闭的模型线
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function GetLoopCurve() As CurveLoop
        Dim Boundaries As List(Of Reference) = New UIDocument(Doc).Selection.PickObjects(ObjectType.Element, New CurveSelectionFilter, "选择一组封闭的模型线。")
        Dim cvLoop As New CurveLoop
        Try
            If Boundaries.Count = 1 Then  ' 要么是封闭的圆或圆弧，要么就不封闭
                Dim c As Curve = DirectCast(Doc.GetElement(Boundaries.Item(0)), ModelCurve).GeometryCurve
                If ((TypeOf c Is Arc) OrElse (TypeOf c Is Ellipse)) AndAlso (Not c.IsBound) Then
                    cvLoop.Append(c)
                Else
                    Throw New Exception("选择的一条圆弧线或者椭圆线并不封闭。")
                End If
            Else
                ' 对于选择了多条曲线的情况
                Dim cs As List(Of Curve) = GeoHelper.GetContiguousCurvesFromSelectedCurveElements(Doc, Boundaries)
                For Each c As Curve In cs
                    cvLoop.Append(c)
                Next
                If cvLoop.IsOpen OrElse Not cvLoop.HasPlane Then
                    Throw New Exception
                Else
                    Return cvLoop
                End If
            End If
        Catch ex As Exception
            Dim res As DialogResult = MessageBox.Show("所选择的曲线不在同一个平面上，或者不能形成封闭的曲线，请重新选择。" & vbCrLf & ex.Message, "Warnning", MessageBoxButtons.OKCancel)
            If res = DialogResult.OK Then
                cvLoop = GetLoopCurve()
            Else
                cvLoop = New CurveLoop
                Return cvLoop
            End If
        End Try
        Return cvLoop
    End Function
    ''' <summary>
    ''' 曲线选择过滤器
    ''' </summary>
    ''' <remarks></remarks>
    Private Class CurveSelectionFilter
        Implements ISelectionFilter
        Public Function AllowElement(element As Element) As Boolean Implements ISelectionFilter.AllowElement
            Dim bln As Boolean = False
            If (TypeOf element Is ModelCurve) Then
                Return True
            End If
            Return bln
        End Function

        Public Function AllowReference(refer As Reference, point As XYZ) As Boolean Implements ISelectionFilter.AllowReference
            Return False
        End Function
    End Class

    ''' <summary>
    ''' 绘制拉伸实体，并将其深度值与具体参数关联起来。
    ''' </summary>
    ''' <param name="FamDoc">实体所在的族文档，此文档当前已经处于打开状态。</param>
    ''' <param name="CurveAA">用来绘制实体的闭合轮廓</param>
    ''' <param name="Depth">模型土体的深度，单位为m，数值为正表示向下的深度，反之表示向上的高度。</param>
    ''' <remarks></remarks>
    Private Sub ExtrusionAndBindingDimension(ByVal FamDoc As Document, ByVal CurveAA As CurveArrArray, ByVal Depth As Double)
        Depth = UnitUtils.ConvertToInternalUnits(Depth, DisplayUnitType.DUT_METERS)  ' 将米转换为英尺。
        Dim BtPoint As New XYZ(0, 0, -Depth)
        ' 定义参考平面
        Dim P_Top As New Plane(New XYZ(1, 0, 0), New XYZ(0, 1, 0), New XYZ(0, 0, 5))
        Dim RefP_Top As ReferencePlane

        ' 在族文档中绘制实体
        Using t As New Transaction(FamDoc, "添加实体")
            t.Start()
            '  Try
            Dim V As View = rvtTools_ez.rvtTools.FindElement(FamDoc, GetType(View), "前", BuiltInCategory.OST_Views)  ' 定义视图
            If V Is Nothing Then
                Throw New NullReferenceException("当前视图为空。")
            End If

            With P_Top
                RefP_Top = FamDoc.FamilyCreate.NewReferencePlane(.Origin, .Origin + .XVec, .YVec, V)
                RefP_Top.Name = "SoilTop"
            End With

            Dim FamilyCreation As Creation.FamilyItemFactory = FamDoc.FamilyCreate

            ' 创建拉伸实体
            Dim sp As SketchPlane = SketchPlane.Create(FamDoc, P_Top)  ' P_Top 为坐标原点所在的水平面
            Dim extru As Extrusion = FamilyCreation.NewExtrusion(True, CurveAA, sp, -10)

            Dim lp As XYZ
            lp = extru.BoundingBox(V).Min
            MessageBox.Show(lp.ToString)

            ' 将拉伸实体的顶面与参数平面进行绑定
            ElementTransformUtils.MoveElement(FamDoc, extru.Id, New XYZ(0, 0, 0))

            Dim Ftop As PlanarFace = GeoHelper.FindFace(extru, New XYZ(0, 0, 1))

            MessageBox.Show(Ftop.Origin.ToString)

            lp = extru.BoundingBox(V).Min
            MessageBox.Show(lp.ToString)

            '   FamilyCreation.NewAlignment(V, RefP_Top.GetReference, Ftop.Reference)

            '' 添加深度参数
            Dim FM As FamilyManager = FamDoc.FamilyManager
            Dim Para_Depth As FamilyParameter = FM.AddParameter("Depth", parameterGroup:=BuiltInParameterGroup.PG_GEOMETRY, _
                                                                 parameterType:=ParameterType.Length, isInstance:=False)
            '' give initial values
            ' FM.Set(Para_Depth, Depth)  ' 这里不知为何为给出报错：InvalidOperationException:There is no current type.


            ' 添加标注
            Dim TopFace As PlanarFace = GeoHelper.FindFace(extru, New XYZ(0, 0, 1))
            Dim BotFace As PlanarFace = GeoHelper.FindFace(extru, New XYZ(0, 0, -1))

            ' make an array of references
            Dim refArray As New ReferenceArray
            refArray.Append(TopFace.Reference)
            refArray.Append(BotFace.Reference)
            ' define a demension line
            Dim a = GeoHelper.FindFace(extru, New XYZ(0, 0, 1)).Origin
            Dim DimLine As Line = Line.CreateBound(TopFace.Origin, BotFace.Origin)
            ' create a dimension
            Dim DimDepth As Dimension = FamilyCreation.NewDimension(V, DimLine, refArray)

            ' 将深度参数与其拉伸实体的深度值关联起来
            DimDepth.FamilyLabel = Para_Depth

            t.Commit()
            'Catch ex As Exception
            '    MessageBox.Show("创建拉伸实体与参数关联出错。" & vbCrLf & ex.Message & vbCrLf & ex.TargetSite.Name, "Error", _
            '                    MessageBoxButtons.OK, MessageBoxIcon.Error)
            '    t.RollBack()
            'End Try
        End Using

    End Sub

    ''' <summary>
    ''' 在当前模型文档中，构造出一个有效的名称，来供开挖土体族使用。
    ''' </summary>
    ''' <param name="doc"></param>
    ''' <param name="ExcavationCompleteDate">日期的数据中请保证不包含小时或者更小的值，即请用“Date对象.Date”来进行赋值。</param>
    ''' <returns></returns>
    ''' <remarks>其基本格式为：开挖土体-2016/04/03-02</remarks>
    Private Function GetValidExcavationSoilName(ByVal doc As Document, ByVal ExcavationCompleteDate As Date)
        Dim prefix As String = "开挖土体-"
        Dim Fams As List(Of Family) = doc.FindFamilies(BuiltInCategory.OST_Site)

        ' 构造一个字典，其中包括的所有开挖土体族的名称中，每一个日期，以及对应的可能编号
        Dim Date_Count As New Dictionary(Of Date, List(Of UShort))

        '
        Dim dt As Date
        Dim Id As Integer
        Dim FamName As String
        '
        For Each f As Family In Fams
            FamName = f.Name
            If FamName.StartsWith(prefix) Then  ' 说明可能是开挖土体
                FamName = FamName.Substring(prefix.Length)
                Dim Cps As String() = FamName.Split("-")
                If Cps.Count > 0 Then
                    If Date.TryParse(Cps(0), dt) Then     ' 说明找到了一个开挖土体族，其对应的日期为dt
                        Dim Ids As List(Of UShort)

                        If Date_Count.Keys.Contains(dt) Then  ' 说明此日期前面已经出现过了
                            Ids = Date_Count.Item(dt) ' 返回日期所对应的值
                            If Ids Is Nothing Then   ' 字典中的集合可能还未初始化
                                Ids = New List(Of UShort)
                                Date_Count.Item(dt) = Ids
                            End If

                        Else  ' 说明此日期第一次出现
                            Ids = New List(Of UShort)
                            Date_Count.Add(dt, Ids)
                        End If

                        If (Cps.Count = 2) AndAlso (UShort.TryParse(Cps(1), Id)) Then  ' 说明族名称中同时记录了日期与序号
                            Ids.Add(Id)
                        Else ' 说明族名称中只记录了日期
                            Ids.Add(0)
                        End If
                    End If
                End If
            End If
        Next

        '
        Dim NewName As String = prefix & Date.Today.Date.ToString("yyyy/MM/dd")  ' 先赋一个初值，避免出现问题

        If Not Date_Count.Keys.Contains(ExcavationCompleteDate) Then
            NewName = prefix & ExcavationCompleteDate.ToString("yyyy/MM/dd")
        Else
            Dim maxId As UShort = Date_Count.Item(ExcavationCompleteDate).Max + 1
            NewName = prefix & ExcavationCompleteDate.ToString("yyyy/MM/dd") & "-" & maxId.ToString("00")
        End If
        Return NewName
    End Function

#End Region

#Region "   ---    Document中的土体单元搜索"

    Private F_SoilElement As Soil_Model
    ''' <summary>
    ''' 模型中的土体单元，此单元与土体
    ''' </summary>
    ''' <param name="SoilElementId">可能的土体单元的ElementId值，如果没有待选的，可以不指定，此时程序会在整个Document中进行搜索。</param>
    ''' <returns>如果成功搜索到，则返回对应的土体单元，如果没有找到，则返回Nothing</returns>
    ''' <remarks></remarks>
    Public Function GetSoilModel(Optional SoilElementId As Integer = -1) As Soil_Model
        Dim FMessage As String = Nothing
        If F_SoilElement Is Nothing Then
            F_SoilElement = FindSoilElement(Doc, New ElementId(SoilElementId), FMessage)
        Else  ' 检查现有的这个模型土体单元是否还有效
            If Not Soil_Model.IsSoildModel(Doc, F_SoilElement.Soil.Id, FMessage) Then
                F_SoilElement = Nothing
            End If
        End If
        If F_SoilElement Is Nothing Then
            MessageBox.Show("在模型中未找到有效的模型土体单元。" & vbCrLf &
                          FMessage, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End If
        Return F_SoilElement
    End Function

    ''' <summary>
    ''' 找到模型中的开挖土体单元
    ''' </summary>
    ''' <param name="doc">进行土体单元搜索的文档</param>
    ''' <param name="SoilElementId">可能的土体单元的ElementId值</param>
    ''' <returns>如果找到有效的土体单元，则返回对应的Soil_Model，否则返回Nothing</returns>
    ''' <remarks></remarks>
    Private Function FindSoilElement(ByVal doc As Document, SoilElementId As ElementId, Optional ByRef FailureMessage As String = Nothing) As Soil_Model
        Dim Soil As FamilyInstance = Nothing
        If SoilElementId <> ElementId.InvalidElementId Then  ' 说明用户手动指定了土体单元的ElementId，此时需要检测此指定的土体单元是否是有效的土体单元
            Try
                Dim FMessage As String = Nothing
                If Soil_Model.IsSoildModel(doc, SoilElementId, FMessage) Then
                    Soil = DirectCast(doc.GetElement(SoilElementId), FamilyInstance)
                Else
                    Throw New InvalidCastException(FMessage)
                End If
            Catch ex As Exception
                FailureMessage = String.Format("指定的元素Id ({0})不是有效的土体单元。", SoilElementId) & vbCrLf &
                                  ex.Message
                Soil = Nothing
            End Try
        Else  ' 说明用户根本没有指定任何可能的土体单元，此时需要在模型中按特定的方式来搜索出土体单元
            ' 在整个文档中进行搜索
            ' 1. 族名称
            Dim SoilFamily As Family = doc.FindFamily(Constants.FamilyName_Soil)
            If SoilFamily IsNot Nothing Then
                ' 实例的类别
                Dim soils As List(Of ElementId) = SoilFamily.Instances(BuiltInCategory.OST_Site).ToElementIds

                ' 整个模型中只能有一个模型土体对象
                If soils.Count = 0 Then
                    FailureMessage = "模型中没有土体单元"
                ElseIf soils.Count > 1 Then
                    Dim UIDoc As UIDocument = New UIDocument(doc)
                    UIDoc.Selection.SetElementIds(soils)
                    FailureMessage = String.Format("模型中的土体单元数量多于一个，请删除多余的土体单元 ( 族""{0}""的实例对象 )。", Constants.FamilyName_Soil)
                Else
                    Soil = DirectCast(soils.Item(0).Element(doc), FamilyInstance)   ' 找到有效且唯一的土体单元 ^_^
                End If

            End If
        End If
        Dim SM As Soil_Model = Nothing
        If Soil IsNot Nothing Then
            SM = Soil_Model.Create(doc, Soil)
        End If
        Return SM
    End Function

    Public Function FindSoilRemove() As List(Of Family)
        Dim collector As New FilteredElementCollector(Doc)

        collector.OfClass(GetType(FamilySymbol)).OfCategory(BuiltInCategory.OST_Site)
        For Each f As FamilySymbol In collector
            MessageBox.Show(f.Name & vbCrLf & f.FamilyName)
        Next
    End Function

#End Region

    ''' <summary>
    ''' 在Revit的项目浏览器中，土体族位于“族>场地”之中，常规情况下，每一个族中只有一个族类型，
    ''' 因为每一个模型土体或者开挖土体，都是通过唯一的曲线创建出来的（在后期的开发中，可能会将其修改为通过“自适应常规模型”来创建土体。）。
    ''' 当模型使用很长一段时间后，出于各种原因，一个模型中可能有很多的开挖土体族都已经没有实例了，这时就需要将这些没有实例的开挖土体族删除。
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function DeleteEmptySoilFamily() As Boolean
        Dim blnSuc As Boolean = False
        Using t As New Transaction(Doc, "删除文档中没有实例对象的土体族。")
            Try

                Dim Fams As List(Of Family) = Doc.FindFamilies(BuiltInCategory.OST_Site)
                Dim FamsToDelete As New List(Of ElementId)
                Dim DeletedFamilyName As String = ""
                For Each f As Family In Fams
                    If f.Instances(BuiltInCategory.OST_Site).Count = 0 Then
                        FamsToDelete.Add(f.Id)
                        DeletedFamilyName = DeletedFamilyName & f.Name & vbCrLf
                    End If
                Next
                t.Start()
                Doc.Delete(FamsToDelete)
                t.Commit()
                '  MessageBox.Show("删除空的土体族对象成功。删除掉的族对象有：" & vbCrLf & DeletedFamilyName, "恭喜", MessageBoxButtons.OK, MessageBoxIcon.None)
                blnSuc = True
            Catch ex As Exception
                MessageBox.Show("删除空的土体族对象失败" & vbCrLf & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                t.RollBack()
                blnSuc = False
            End Try
        End Using


        Return blnSuc
    End Function

End Class