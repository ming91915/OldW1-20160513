﻿Imports System
Imports Autodesk.Revit.UI
Imports Autodesk.Revit.DB
Imports Forms = System.Windows.Forms
Imports OldW.Soil
Imports std_ez

Public Class frm_DrawExcavation
    Implements IExternalEventHandler

#Region "   ---   Declarations"

#Region "   ---   Types"

    ''' <summary>
    ''' ModelessForm的操作需求，用来从窗口向IExternalEventHandler对象传递需求。
    ''' </summary>
    ''' <remarks></remarks>
    Private Enum Request
        Draw
    End Enum

#End Region

#Region "   ---   Fields"


    ''' <summary>用来触发外部事件（通过其Raise方法） </summary>
    ''' <remarks>ExEvent属性是必须有的，它用来执行Raise方法以触发事件。</remarks>
    Private ExEvent As ExternalEvent

    ''' <summary> Execute方法所要执行的需求 </summary>
    ''' <remarks>在Form中要执行某一个操作时，先将对应的操作需求信息赋值为一个RequestId枚举值，然后再执行ExternalEvent.Raise()方法。
    ''' 然后Revit会在会在下个闲置时间（idling time cycle）到来时调用IExternalEventHandler.Excute方法，在这个Execute方法中，
    ''' 再通过RequestId来提取对应的操作需求，</remarks>
    Private RequestId As Request

    Public Document As Document

    Public ExcavDoc As ExcavationDoc

    ''' <summary> 要绘制的模型的深度，单位为m </summary>
    Private Depth As Double

    ''' <summary> 开挖土体开挖完成的日期 </summary>
    Private CompletedDate As Date

    ''' <summary> 开挖土体开始开挖的日期 </summary>
    Private StartedDate As Date

#End Region

#End Region

#Region "   ---   构造函数与窗口的打开关闭"

    Public Sub New(ByVal ExcavDoc As ExcavationDoc)
        ' This call is required by the designer.
        InitializeComponent()
        ' Add any initialization after the InitializeComponent() call.
        '' ----------------------

        Me.TopMost = True
        Me.StartPosition = FormStartPosition.CenterScreen


        ' 参数绑定
        LabelCompletedDate.DataBindings.Add("Enabled", RadioBtn_ExcavSoil, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)
        btn__DateCalendar.DataBindings.Add("Enabled", RadioBtn_ExcavSoil, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)
        TextBox_StartedDate.DataBindings.Add("Enabled", RadioBtn_ExcavSoil, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)
        TextBox_CompletedDate.DataBindings.Add("Enabled", RadioBtn_ExcavSoil, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)
        LabelSides.DataBindings.Add("Enabled", RadioBtn_Polygon, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)
        ComboBox_sides.DataBindings.Add("Enabled", RadioBtn_Polygon, "Checked", False, DataSourceUpdateMode.OnPropertyChanged)

        '
        Me.ExcavDoc = ExcavDoc
        Me.Document = ExcavDoc.Document

        '' ------ 将所有的初始化工作完成后，执行外部事件的绑定 ----------------

        ' 新建一个外部事件实例
        Me.ExEvent = ExternalEvent.Create(Me)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ' 保存的实例需要进行释放
        Me.ExEvent.Dispose()
        Me.ExEvent = Nothing
        ' 不关闭，只隐藏
        MyBase.Hide()
    End Sub


    Public Function GetName() As String Implements IExternalEventHandler.GetName
        Return "Revit External Event Example"
    End Function

#End Region

#Region "   ---   执行操作 ExternalEvent.Raise 与 IExternalEventHandler.Execute"

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles BtnDraw.Click
        Me.RequestId = Request.Draw
        Me.ExEvent.Raise()
        Me.DozeOff()
    End Sub

    ''' <summary>
    ''' 在执行ExternalEvent.Raise()方法之前，请先将操作需求信息赋值给其RequestHandler对象的RequestId属性。
    ''' 当ExternalEvent.Raise后，Revit会在下个闲置时间（idling time cycle）到来时调用IExternalEventHandler.Execute方法的实现。
    ''' </summary>
    ''' <param name="app">此属性由Revit自动提供，其值不是Nothing，而是一个真实的UIApplication对象</param>
    ''' <remarks>由于在通过外部程序所引发的操作中，如果出现异常，Revit并不会给出任何提示或者报错，
    ''' 而是直接退出函数。所以要将整个操作放在一个Try代码块中，以处理可能出现的任何报错。</remarks>
    Public Sub Execute(app As UIApplication) Implements IExternalEventHandler.Execute
        Try  ' 由于在通过外部程序所引发的操作中，如果出现异常，Revit并不会给出任何提示或者报错，而是直接退出函数。所以这里要将整个操作放在一个Try代码块中，以处理可能出现的任何报错。

            Dim Doc As Document = Me.Document
            Dim uiDoc As UIDocument = New UIDocument(Doc)

            ' 开始执行具体的操作
            Select Case RequestId  ' 判断具体要干什么
                Case Request.Draw

                    ' 提取开挖深度
                    If Me.Depth = 0 Then
                        MessageBox.Show("深度值不能为0。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Error)
                        Exit Sub
                    End If

                    Dim soil As Soil_Model
                    If Me.RadioBtn_ExcavSoil.Checked Then   ' 绘制开挖土体

                        soil = Me.ExcavDoc.FindSoilModel()
                        Dim DesiredName As String = ""
                        Dim strDate As String
                        ' 提取开始开挖的时间
                        Dim blnHasStartedDate As Boolean = False
                        Dim blnHasCompletedDate As Boolean = False

                        strDate = Me.TextBox_StartedDate.Text
                        If Not String.IsNullOrEmpty(strDate) Then
                            If Utils.String2Date(strDate, Me.StartedDate) Then  ' 说明不能直接转化为日期
                                blnHasStartedDate = True
                            Else
                                MessageBox.Show("请输入正确格式的开挖完成日期。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                Exit Sub
                            End If
                        End If

                        strDate = Me.TextBox_CompletedDate.Text
                        If Not String.IsNullOrEmpty(strDate) Then
                            If Utils.String2Date(strDate, Me.CompletedDate) Then  ' 说明不能直接转化为日期
                                blnHasCompletedDate = True
                                DesiredName = Me.CompletedDate.ToShortDateString
                            Else
                                MessageBox.Show("请输入正确格式的开挖完成日期。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                Exit Sub
                            End If
                        End If
                        ' 绘制开挖模型
                        Dim ex As Soil_Excav = Me.ExcavDoc.CreateExcavationSoil(soil, Me.Depth, True, DesiredName)

                        ' 设置开挖开始或者完成的时间
                        If blnHasStartedDate OrElse blnHasCompletedDate Then
                            Using t As New Transaction(Doc, "设置开挖开始或者完成的时间")
                                t.Start()
                                If blnHasStartedDate Then
                                    ex.SetExcavatedDate(t, True, StartedDate)
                                End If
                                If blnHasCompletedDate Then
                                    ex.SetExcavatedDate(t, False, CompletedDate)
                                End If
                                t.Commit()
                            End Using
                        End If

                        ' 将开挖土体在模型土体中隐藏起来
                        soil.RemoveSoil(ex)

                    Else    ' 绘制模型土体
                        soil = Me.ExcavDoc.CreateModelSoil(Me.Depth, True)

                    End If
            End Select
        Catch ex As Exception
            MessageBox.Show("出错" & vbCrLf & ex.Message & vbCrLf & ex.TargetSite.Name & vbCrLf & ex.StackTrace,
                            "外部事件执行出错", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            ' 刷新Form，将Form中的Controls的Enable属性设置为True
            Me.WarmUp()
        End Try
    End Sub

#End Region

#Region "   ---   界面效果与事件响应"

    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox_Depth.TextChanged
        If Not Double.TryParse(TextBox_Depth.Text, Depth) Then
            TextBox_Depth.Text = ""
        End If
    End Sub

    ''' <summary> 在Revit执行相关操作时，禁用窗口中的控件 </summary>
    Private Sub DozeOff()
        Me.BtnDraw.Enabled = False

    End Sub

    ''' <summary> 在外部事件RequestHandler中的Execute方法执行完成后，用来激活窗口中的控件 </summary>
    Private Sub WarmUp()
        For Each c As Forms.Control In Me.Controls
            c.Enabled = True
        Next

    End Sub

#End Region

End Class