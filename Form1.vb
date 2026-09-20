Imports System
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms

Namespace AgriExpertSystem

    ''' <summary>
    ''' AXPERT-Mini
    ''' A single-window desktop demonstration of the "Integrated Information System
    ''' to Facilitate Farmers" pipeline:
    '''   Farmer uploads a leaf image  -->  Expert System processes it
    '''   (grayscale / edge detection / binarization / emboss)
    '''   -->  disease is identified from a simple per-crop rule base
    '''   -->  a report is generated  -->  Agriculture Expert verifies it
    '''   -->  the solution is saved / "sent" back to the farmer.
    ''' Supports Rice, Wheat and Sugarcane.
    ''' </summary>
    Public Class Form1
        Inherits Form

        ' ---------------- Controls ----------------
        Private txtUsername As TextBox
        Private cboCrop As ComboBox
        Private btnUpload As Button
        Private lblImagePath As Label
        Private btnProcess As Button
        Private btnDetect As Button
        Private btnGenerateReport As Button
        Private btnSaveReport As Button
        Private chkExpertVerified As CheckBox
        Private txtDiseaseName As TextBox
        Private txtDiseaseInfo As TextBox
        Private txtReport As TextBox

        Private pbOriginal, pbGray, pbEdge, pbBinary, pbEmboss As PictureBox
        Private lblOriginal, lblGray, lblEdge, lblBinary, lblEmboss As Label

        ' ---------------- State ----------------
        Private originalBitmap As Bitmap
        Private grayBitmap As Bitmap
        Private edgeBitmap As Bitmap
        Private binaryBitmap As Bitmap
        Private embossBitmap As Bitmap
        Private spotPercentage As Double

        Private ReadOnly diseaseDB As New Dictionary(Of String, List(Of DiseaseInfo))

        ''' <summary>Simple record describing a disease bucket for a crop.</summary>
        Private Class DiseaseInfo
            Public Property Name As String
            Public Property MinPct As Double
            Public Property MaxPct As Double
            Public Property Description As String
            Public Sub New(n As String, mn As Double, mx As Double, d As String)
                Name = n
                MinPct = mn
                MaxPct = mx
                Description = d
            End Sub
        End Class

        Public Sub New()
            InitializeUI()
            LoadDiseaseDatabase()
        End Sub

        ''' <summary>
        ''' Very small rule base used to turn a lesion-area percentage into a
        ''' plausible diagnosis for demo purposes. Replace with a trained
        ''' classifier (e.g. a CNN) for real-world use.
        ''' </summary>
        Private Sub LoadDiseaseDatabase()
            diseaseDB("Rice") = New List(Of DiseaseInfo) From {
                New DiseaseInfo("Healthy", 0, 3, "No significant lesion area detected. Leaf appears healthy."),
                New DiseaseInfo("Brown Spot", 3, 12, "Small brown, oval lesions scattered across the leaf blade; often linked to nitrogen-deficient soil."),
                New DiseaseInfo("Bacterial Leaf Blight", 12, 25, "Water-soaked lesions turning yellow-white along the leaf margins, caused by Xanthomonas oryzae."),
                New DiseaseInfo("Rice Blast", 25, 100, "Diamond-shaped lesions with grey centers and brown margins, caused by Magnaporthe oryzae.")
            }
            diseaseDB("Wheat") = New List(Of DiseaseInfo) From {
                New DiseaseInfo("Healthy", 0, 3, "No significant lesion area detected. Leaf appears healthy."),
                New DiseaseInfo("Powdery Mildew", 3, 10, "White powdery fungal growth on the leaf surface, favored by cool, humid conditions."),
                New DiseaseInfo("Yellow Rust", 10, 20, "Yellow-orange pustules arranged in stripes along the leaf veins."),
                New DiseaseInfo("Brown / Leaf Rust", 20, 100, "Reddish-brown circular pustules scattered over the leaf surface.")
            }
            diseaseDB("Sugarcane") = New List(Of DiseaseInfo) From {
                New DiseaseInfo("Healthy", 0, 3, "No significant lesion area detected. Leaf appears healthy."),
                New DiseaseInfo("Yellow Spot", 3, 10, "Small yellowish spots with reddish borders on the upper leaf surface."),
                New DiseaseInfo("Ring Spot", 10, 22, "Oval, reddish-brown lesions with a distinct water-soaked ring, common in humid weather."),
                New DiseaseInfo("Eye Spot", 22, 100, "Elongated, eye-shaped lesions with a light-brown center and a dark border.")
            }
        End Sub

        ' =====================================================================
        ' UI LAYOUT
        ' =====================================================================
        Private Sub InitializeUI()
            Me.Text = "AXPERT-Mini : Agriculture Expert System (Rice / Wheat / Sugarcane)"
            Me.Width = 1150
            Me.Height = 720
            Me.StartPosition = FormStartPosition.CenterScreen
            Me.MaximizeBox = False
            Me.FormBorderStyle = FormBorderStyle.FixedSingle

            ' ---- Top input row ----
            Dim lblUser As New Label With {.Text = "Farmer Username:", .Left = 15, .Top = 15, .Width = 120}
            Me.Controls.Add(lblUser)
            txtUsername = New TextBox With {.Left = 140, .Top = 12, .Width = 150}
            Me.Controls.Add(txtUsername)

            Dim lblCrop As New Label With {.Text = "Crop:", .Left = 310, .Top = 15, .Width = 40}
            Me.Controls.Add(lblCrop)
            cboCrop = New ComboBox With {.Left = 355, .Top = 12, .Width = 120, .DropDownStyle = ComboBoxStyle.DropDownList}
            cboCrop.Items.AddRange(New String() {"Rice", "Wheat", "Sugarcane"})
            cboCrop.SelectedIndex = 0
            Me.Controls.Add(cboCrop)

            btnUpload = New Button With {.Text = "Upload Leaf Image", .Left = 490, .Top = 10, .Width = 150}
            AddHandler btnUpload.Click, AddressOf btnUpload_Click
            Me.Controls.Add(btnUpload)

            lblImagePath = New Label With {.Text = "No image selected", .Left = 650, .Top = 15, .Width = 300, .ForeColor = Color.Gray}
            Me.Controls.Add(lblImagePath)

            btnProcess = New Button With {.Text = "Analyze Image", .Left = 960, .Top = 10, .Width = 150, .Enabled = False}
            AddHandler btnProcess.Click, AddressOf btnProcess_Click
            Me.Controls.Add(btnProcess)

            ' ---- Picture box row (mirrors the thesis "Detection System" figure) ----
            Dim picTop As Integer = 75
            Dim picSize As Integer = 190
            Dim gap As Integer = 20
            Dim startLeft As Integer = 15

            CreatePictureSlot("Original", startLeft + (picSize + gap) * 0, picTop, picSize, pbOriginal, lblOriginal)
            CreatePictureSlot("Grayscale", startLeft + (picSize + gap) * 1, picTop, picSize, pbGray, lblGray)
            CreatePictureSlot("Edge Detection", startLeft + (picSize + gap) * 2, picTop, picSize, pbEdge, lblEdge)
            CreatePictureSlot("Binarized", startLeft + (picSize + gap) * 3, picTop, picSize, pbBinary, lblBinary)
            CreatePictureSlot("Emboss Filter", startLeft + (picSize + gap) * 4, picTop, picSize, pbEmboss, lblEmboss)

            Dim panelTop As Integer = picTop + picSize + 45

            ' ---- Diagnosis row ----
            btnDetect = New Button With {.Text = "Identify Disease", .Left = 15, .Top = panelTop, .Width = 150, .Enabled = False}
            AddHandler btnDetect.Click, AddressOf btnDetect_Click
            Me.Controls.Add(btnDetect)

            Dim lblDName As New Label With {.Text = "Detected Disease:", .Left = 180, .Top = panelTop + 3, .Width = 120}
            Me.Controls.Add(lblDName)
            txtDiseaseName = New TextBox With {.Left = 305, .Top = panelTop, .Width = 200, .ReadOnly = True}
            Me.Controls.Add(txtDiseaseName)

            Dim lblDInfo As New Label With {.Text = "Disease Description:", .Left = 15, .Top = panelTop + 35, .Width = 160}
            Me.Controls.Add(lblDInfo)
            txtDiseaseInfo = New TextBox With {.Left = 15, .Top = panelTop + 58, .Width = 490, .Height = 80, .Multiline = True, .ReadOnly = True}
            Me.Controls.Add(txtDiseaseInfo)

            chkExpertVerified = New CheckBox With {.Text = "Agriculture Expert has verified this diagnosis", .Left = 520, .Top = panelTop + 3, .Width = 340}
            Me.Controls.Add(chkExpertVerified)

            btnGenerateReport = New Button With {.Text = "Generate Report", .Left = 520, .Top = panelTop + 35, .Width = 150, .Enabled = False}
            AddHandler btnGenerateReport.Click, AddressOf btnGenerateReport_Click
            Me.Controls.Add(btnGenerateReport)

            btnSaveReport = New Button With {.Text = "Send Solution to Farmer", .Left = 680, .Top = panelTop + 35, .Width = 220, .Enabled = False}
            AddHandler btnSaveReport.Click, AddressOf btnSaveReport_Click
            Me.Controls.Add(btnSaveReport)

            ' ---- Report box ----
            Dim lblReport As New Label With {.Text = "Computer Generated Report:", .Left = 15, .Top = panelTop + 150, .Width = 300}
            Me.Controls.Add(lblReport)
            txtReport = New TextBox With {
                .Left = 15, .Top = panelTop + 173, .Width = 1100, .Height = 130,
                .Multiline = True, .ReadOnly = True, .ScrollBars = ScrollBars.Vertical,
                .Font = New Font("Consolas", 9)
            }
            Me.Controls.Add(txtReport)
        End Sub

        Private Sub CreatePictureSlot(caption As String, leftPos As Integer, topPos As Integer, boxSize As Integer, ByRef pb As PictureBox, ByRef lbl As Label)
            lbl = New Label With {.Text = caption, .Left = leftPos, .Top = topPos - 18, .Width = boxSize, .TextAlign = ContentAlignment.MiddleCenter}
            Me.Controls.Add(lbl)
            pb = New PictureBox With {
                .Left = leftPos, .Top = topPos, .Width = boxSize, .Height = boxSize,
                .BorderStyle = BorderStyle.FixedSingle, .SizeMode = PictureBoxSizeMode.Zoom,
                .BackColor = Color.WhiteSmoke
            }
            Me.Controls.Add(pb)
        End Sub

        ' =====================================================================
        ' EVENT HANDLERS
        ' =====================================================================
        Private Sub btnUpload_Click(sender As Object, e As EventArgs)
            Using ofd As New OpenFileDialog()
                ofd.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp"
                If ofd.ShowDialog() = DialogResult.OK Then
                    Try
                        originalBitmap = New Bitmap(ofd.FileName)
                        pbOriginal.Image = originalBitmap
                        lblImagePath.Text = Path.GetFileName(ofd.FileName)
                        btnProcess.Enabled = True
                        ClearDownstream()
                    Catch ex As Exception
                        MessageBox.Show("Could not load image: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End If
            End Using
        End Sub

        Private Sub ClearDownstream()
            pbGray.Image = Nothing
            pbEdge.Image = Nothing
            pbBinary.Image = Nothing
            pbEmboss.Image = Nothing
            txtDiseaseName.Clear()
            txtDiseaseInfo.Clear()
            txtReport.Clear()
            chkExpertVerified.Checked = False
            btnDetect.Enabled = False
            btnGenerateReport.Enabled = False
            btnSaveReport.Enabled = False
        End Sub

        Private Sub btnProcess_Click(sender As Object, e As EventArgs)
            If originalBitmap Is Nothing Then Return
            Me.Cursor = Cursors.WaitCursor
            Try
                grayBitmap = ToGrayscale(originalBitmap)
                edgeBitmap = SobelEdge(grayBitmap)
                binaryBitmap = Binarize(grayBitmap, 110)
                embossBitmap = Emboss(grayBitmap)

                pbGray.Image = grayBitmap
                pbEdge.Image = edgeBitmap
                pbBinary.Image = binaryBitmap
                pbEmboss.Image = embossBitmap

                spotPercentage = ComputeDarkSpotPercentage(binaryBitmap)
                btnDetect.Enabled = True
            Catch ex As Exception
                MessageBox.Show("Image processing failed: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Finally
                Me.Cursor = Cursors.Default
            End Try
        End Sub

        Private Sub btnDetect_Click(sender As Object, e As EventArgs)
            Dim crop As String = cboCrop.SelectedItem.ToString()
            Dim list = diseaseDB(crop)
            Dim match As DiseaseInfo = list(0)
            For Each d In list
                If spotPercentage >= d.MinPct AndAlso spotPercentage < d.MaxPct Then
                    match = d
                    Exit For
                End If
            Next
            txtDiseaseName.Text = match.Name
            txtDiseaseInfo.Text = match.Description
            btnGenerateReport.Enabled = True
        End Sub

        Private Sub btnGenerateReport_Click(sender As Object, e As EventArgs)
            Dim sb As New StringBuilder()
            sb.AppendLine("=========================================")
            sb.AppendLine("   AXPERT-MINI : COMPUTER GENERATED REPORT")
            sb.AppendLine("=========================================")
            sb.AppendLine("Date/Time       : " & DateTime.Now.ToString())
            sb.AppendLine("Farmer          : " & If(String.IsNullOrWhiteSpace(txtUsername.Text), "N/A", txtUsername.Text))
            sb.AppendLine("Crop            : " & cboCrop.SelectedItem.ToString())
            sb.AppendLine("Image File      : " & lblImagePath.Text)
            sb.AppendLine("Lesion Area %   : " & spotPercentage.ToString("0.00") & "%")
            sb.AppendLine("Detected Issue  : " & txtDiseaseName.Text)
            sb.AppendLine("Description     : " & txtDiseaseInfo.Text)
            sb.AppendLine("Expert Verified : " & If(chkExpertVerified.Checked, "YES", "PENDING"))
            sb.AppendLine("=========================================")
            txtReport.Text = sb.ToString()
            btnSaveReport.Enabled = True
        End Sub

        Private Sub btnSaveReport_Click(sender As Object, e As EventArgs)
            If Not chkExpertVerified.Checked Then
                If MessageBox.Show("This report has not been verified by an Agriculture Expert yet. Send anyway?",
                                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.No Then
                    Return
                End If
            End If
            Try
                Dim folder As String = Path.Combine(Application.StartupPath, "Reports")
                Directory.CreateDirectory(folder)
                Dim stamp As String = DateTime.Now.ToString("yyyyMMdd_HHmmss")
                Dim safeUser As String = If(String.IsNullOrWhiteSpace(txtUsername.Text), "farmer", txtUsername.Text)
                Dim baseName As String = $"{safeUser}_{cboCrop.SelectedItem}_{stamp}"

                File.WriteAllText(Path.Combine(folder, baseName & ".txt"), txtReport.Text)
                If originalBitmap IsNot Nothing Then originalBitmap.Save(Path.Combine(folder, baseName & "_original.png"), ImageFormat.Png)
                If binaryBitmap IsNot Nothing Then binaryBitmap.Save(Path.Combine(folder, baseName & "_binary.png"), ImageFormat.Png)

                MessageBox.Show("Solution sent to farmer's account." & vbCrLf & "Saved to: " & folder, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show("Could not save report: " & ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Sub

        ' =====================================================================
        ' IMAGE PROCESSING PIPELINE
        ' (Grayscale -> Sobel Edge -> Binarization -> Emboss, as in the thesis
        '  "Detection System" / "HIS Algorithm" figures)
        ' =====================================================================

        Private Function NormalizeTo24bpp(src As Bitmap) As Bitmap
            Dim bmp As New Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb)
            Using g = Graphics.FromImage(bmp)
                g.DrawImage(src, 0, 0, src.Width, src.Height)
            End Using
            Return bmp
        End Function

        Private Function GetBytes(bmp As Bitmap, ByRef stride As Integer) As Byte()
            Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
            Dim bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb)
            stride = bmpData.Stride
            Dim bytes(stride * bmp.Height - 1) As Byte
            Marshal.Copy(bmpData.Scan0, bytes, 0, bytes.Length)
            bmp.UnlockBits(bmpData)
            Return bytes
        End Function

        Private Function BytesToBitmap(bytes As Byte(), width As Integer, height As Integer, stride As Integer) As Bitmap
            Dim bmp As New Bitmap(width, height, PixelFormat.Format24bppRgb)
            Dim rect As New Rectangle(0, 0, width, height)
            Dim bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb)
            Marshal.Copy(bytes, 0, bmpData.Scan0, bytes.Length)
            bmp.UnlockBits(bmpData)
            Return bmp
        End Function

        Private Function ToGrayscale(src As Bitmap) As Bitmap
            Dim norm = NormalizeTo24bpp(src)
            Dim stride As Integer
            Dim data = GetBytes(norm, stride)
            Dim w = norm.Width
            Dim h = norm.Height
            For y = 0 To h - 1
                Dim row = y * stride
                For x = 0 To w - 1
                    Dim idx = row + x * 3
                    Dim b = data(idx)
                    Dim g = data(idx + 1)
                    Dim r = data(idx + 2)
                    Dim gray As Byte = CByte(0.299 * r + 0.587 * g + 0.114 * b)
                    data(idx) = gray
                    data(idx + 1) = gray
                    data(idx + 2) = gray
                Next
            Next
            Return BytesToBitmap(data, w, h, stride)
        End Function

        Private Function SobelEdge(grayBmp As Bitmap) As Bitmap
            Dim stride As Integer
            Dim data = GetBytes(grayBmp, stride)
            Dim w = grayBmp.Width
            Dim h = grayBmp.Height
            Dim outData(data.Length - 1) As Byte
            Dim gx() As Integer = {-1, 0, 1, -2, 0, 2, -1, 0, 1}
            Dim gy() As Integer = {-1, -2, -1, 0, 0, 0, 1, 2, 1}

            For y = 1 To h - 2
                For x = 1 To w - 2
                    Dim sumX As Integer = 0
                    Dim sumY As Integer = 0
                    Dim k As Integer = 0
                    For j = -1 To 1
                        For i = -1 To 1
                            Dim idx = (y + j) * stride + (x + i) * 3
                            Dim val = data(idx)
                            sumX += gx(k) * val
                            sumY += gy(k) * val
                            k += 1
                        Next
                    Next
                    Dim mag = CInt(Math.Sqrt(sumX * sumX + sumY * sumY))
                    If mag > 255 Then mag = 255
                    Dim outIdx = y * stride + x * 3
                    outData(outIdx) = CByte(mag)
                    outData(outIdx + 1) = CByte(mag)
                    outData(outIdx + 2) = CByte(mag)
                Next
            Next
            Return BytesToBitmap(outData, w, h, stride)
        End Function

        Private Function Binarize(grayBmp As Bitmap, threshold As Integer) As Bitmap
            Dim stride As Integer
            Dim data = GetBytes(grayBmp, stride)
            Dim w = grayBmp.Width
            Dim h = grayBmp.Height
            For y = 0 To h - 1
                For x = 0 To w - 1
                    Dim idx = y * stride + x * 3
                    Dim v As Byte = If(data(idx) < threshold, CByte(0), CByte(255))
                    data(idx) = v
                    data(idx + 1) = v
                    data(idx + 2) = v
                Next
            Next
            Return BytesToBitmap(data, w, h, stride)
        End Function

        Private Function Emboss(grayBmp As Bitmap) As Bitmap
            Dim stride As Integer
            Dim data = GetBytes(grayBmp, stride)
            Dim w = grayBmp.Width
            Dim h = grayBmp.Height
            Dim outData(data.Length - 1) As Byte
            Dim kernel() As Integer = {-2, -1, 0, -1, 1, 1, 0, 1, 2}

            For y = 1 To h - 2
                For x = 1 To w - 2
                    Dim sum As Integer = 0
                    Dim k As Integer = 0
                    For j = -1 To 1
                        For i = -1 To 1
                            Dim idx = (y + j) * stride + (x + i) * 3
                            sum += kernel(k) * data(idx)
                            k += 1
                        Next
                    Next
                    sum += 128
                    If sum < 0 Then sum = 0
                    If sum > 255 Then sum = 255
                    Dim outIdx = y * stride + x * 3
                    outData(outIdx) = CByte(sum)
                    outData(outIdx + 1) = CByte(sum)
                    outData(outIdx + 2) = CByte(sum)
                Next
            Next
            Return BytesToBitmap(outData, w, h, stride)
        End Function

        Private Function ComputeDarkSpotPercentage(binBmp As Bitmap) As Double
            Dim stride As Integer
            Dim data = GetBytes(binBmp, stride)
            Dim w = binBmp.Width
            Dim h = binBmp.Height
            Dim blackCount As Long = 0
            Dim total As Long = CLng(w) * h
            For y = 0 To h - 1
                For x = 0 To w - 1
                    Dim idx = y * stride + x * 3
                    If data(idx) = 0 Then blackCount += 1
                Next
            Next
            If total = 0 Then Return 0
            Return (blackCount / total) * 100.0
        End Function

    End Class

End Namespace
