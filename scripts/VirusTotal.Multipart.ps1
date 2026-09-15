# Explicit multipart headers for the VirusTotal large-file upload parser.
# Keep the boundary unquoted and the form field/filename quoted; stream file bytes unchanged.
function New-VirusTotalMultipart([IO.FileInfo] $File) {
    $boundary = 'PaneShift' + [Guid]::NewGuid().ToString('N')
    $multipart = [Net.Http.MultipartFormDataContent]::new($boundary)
    try {
        $content = [Net.Http.StreamContent]::new($File.OpenRead())
        $multipart.Add($content)
        $content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream')
        $disposition = [Net.Http.Headers.ContentDispositionHeaderValue]::new('form-data')
        $disposition.Name = '"file"'
        $disposition.FileName = '"' + $File.Name.Replace('"', '') + '"'
        $content.Headers.ContentDisposition = $disposition
        $multipart.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::Parse("multipart/form-data; boundary=$boundary")
        return ,$multipart
    } catch { $multipart.Dispose(); throw }
}
