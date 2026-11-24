using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GMAO.API.Controllers;
using GMAO.Application.DTOs.Documents;
using GMAO.Application.Interfaces;
using GMAO.Domain.Entities;
using GMAO.Domain.Enums;
using GMAO.Infrastructure.Data;
using GMAO.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GMAO.Tests;

public class StorageAndMigrationTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;
        public List<(HttpMethod Method, string Url, string? AuthHeader, string? ApiKeyHeader, byte[]? Body)> Requests { get; } = new();

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[]? body = request.Content != null ? await request.Content.ReadAsByteArrayAsync(cancellationToken) : null;
            string? auth = request.Headers.Authorization?.ToString();
            string? apiKey = request.Headers.TryGetValues("apikey", out var vals) ? string.Join(",", vals) : null;
            Requests.Add((request.Method, request.RequestUri?.ToString() ?? "", auth, apiKey, body));
            return await _handler(request);
        }
    }

    private sealed class InMemoryBlobStorageStub : IFileStorageService
    {
        public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new();
        public bool CorruptNextDownload { get; set; }
        public string ProviderName => "Supabase";

        public async Task<StoredFileResult> UploadAsync(Stream fileStream, string fileName, string contentType, int societeId, CancellationToken ct = default)
        {
            var key = FileValidationHelper.BuildTenantStoragePath(societeId, fileName);
            return await UploadAtPathAsync(fileStream, key, contentType, ct);
        }

        public async Task<StoredFileResult> UploadAtPathAsync(Stream fileStream, string storagePath, string contentType, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();
            Blobs[storagePath] = (bytes, contentType);
            return new StoredFileResult
            {
                StoragePath = storagePath,
                Provider = ProviderName,
                BucketName = "gmao-documents",
                FileSizeBytes = bytes.Length,
                ContentType = contentType,
                Sha256Checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()
            };
        }

        public Task<(Stream stream, string contentType, string fileName)?> DownloadAsync(string storagePath, CancellationToken ct = default)
        {
            if (!Blobs.TryGetValue(storagePath, out var blob))
                return Task.FromResult<(Stream, string, string)?>(null);

            var data = CorruptNextDownload ? Encoding.UTF8.GetBytes("CORRUPTED_DATA") : blob.Bytes;
            return Task.FromResult<(Stream, string, string)?>((new MemoryStream(data), blob.ContentType, Path.GetFileName(storagePath)));
        }

        public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult(Blobs.Remove(storagePath));

        public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult(Blobs.ContainsKey(storagePath));

        public Task<StorageFileMetadata?> GetMetadataAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult<StorageFileMetadata?>(Blobs.TryGetValue(storagePath, out var b)
                ? new StorageFileMetadata { Exists = true, StoragePath = storagePath, SizeBytes = b.Bytes.Length, ContentType = b.ContentType }
                : null);

        public Task<SignedUrlResult?> CreateSignedUrlAsync(string storagePath, int expiresInSeconds = 120, CancellationToken ct = default)
            => Task.FromResult<SignedUrlResult?>(new SignedUrlResult
            {
                Url = $"https://rbolwywlpzidkbrczcww.supabase.co/storage/v1/object/sign/gmao-documents/{storagePath}?token=test",
                ExpiresInSeconds = expiresInSeconds,
                IsExternalSignedUrl = true,
                Provider = ProviderName
            });
    }

    [Fact]
    public async Task FileValidation_ValidPdf_ComputesSha256AndTenantKey()
    {
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");
        await using var input = new MemoryStream(pdfBytes);

        var (buffered, sha256, mime) = await FileValidationHelper.ValidateSignatureAndHashAsync(
            input, "manuel_chaudiere.pdf", "application/pdf");

        await using (buffered)
        {
            Assert.Equal("application/pdf", mime);
            Assert.Equal(64, sha256.Length);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant(), sha256);

            var key = FileValidationHelper.BuildTenantStoragePath(7, "manuel_chaudiere.pdf");
            Assert.StartsWith("tenants/7/documents/", key);
            Assert.EndsWith(".pdf", key);
        }
    }

    [Fact]
    public async Task FileValidation_SpoofedExecutableRenamedAsPdf_IsRejected()
    {
        var exeHeaderBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }; // "MZ" PE executable
        await using var input = new MemoryStream(exeHeaderBytes);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FileValidationHelper.ValidateSignatureAndHashAsync(input, "payload.pdf", "application/pdf"));
    }

    [Fact]
    public async Task SupabaseStorage_Upload_SignUrl_And_OutageHandling()
    {
        var storedObjects = new Dictionary<string, byte[]>();
        bool bucketExists = false;

        var handler = new StubHttpMessageHandler(async req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Get && path.EndsWith("/storage/v1/bucket/gmao-documents"))
                return new HttpResponseMessage(bucketExists ? HttpStatusCode.OK : HttpStatusCode.NotFound);

            if (req.Method == HttpMethod.Post && path.EndsWith("/storage/v1/bucket"))
            {
                var bodyStr = await req.Content!.ReadAsStringAsync();
                Assert.Contains("\"public\":false", bodyStr);
                bucketExists = true;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"name\":\"gmao-documents\"}") };
            }

            if (req.Method == HttpMethod.Post && path.Contains("/storage/v1/object/sign/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"signedURL\":\"/object/sign/gmao-documents/tenants/3/documents/abc.pdf?token=shortlived\"}")
                };
            }

            if (req.Method == HttpMethod.Post && path.Contains("/storage/v1/object/gmao-documents/"))
            {
                var idx = path.IndexOf("/object/gmao-documents/", StringComparison.Ordinal) + "/object/gmao-documents/".Length;
                var key = path[idx..];
                storedObjects[key] = await req.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }

            if (req.Method == HttpMethod.Get && path.Contains("/storage/v1/object/gmao-documents/"))
            {
                var idx = path.IndexOf("/object/gmao-documents/", StringComparison.Ordinal) + "/object/gmao-documents/".Length;
                var key = path[idx..];
                if (!storedObjects.TryGetValue(key, out var bytes))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                var msg = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                msg.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                return msg;
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var options = Options.Create(new SupabaseStorageOptions
        {
            Url = "https://rbolwywlpzidkbrczcww.supabase.co",
            ServiceRoleKey = "fake_test_service_role_key_never_leaked",
            BucketName = "gmao-documents",
            SignedUrlExpiresSeconds = 120
        });

        var httpClient = new HttpClient(handler);
        var service = new SupabaseFileStorageService(httpClient, options, NullLogger<SupabaseFileStorageService>.Instance);

        var payload = Encoding.ASCII.GetBytes("%PDF-1.4\nTest Supabase Blob\n%%EOF");
        await using var uploadStream = new MemoryStream(payload);
        var result = await service.UploadAsync(uploadStream, "notice.pdf", "application/pdf", societeId: 3);

        Assert.Equal("Supabase", result.Provider);
        Assert.Equal("gmao-documents", result.BucketName);
        Assert.StartsWith("tenants/3/documents/", result.StoragePath);
        Assert.DoesNotContain("fake_test_service_role_key", result.StoragePath);
        Assert.Contains(handler.Requests, r => r.AuthHeader == "Bearer fake_test_service_role_key_never_leaked" && r.ApiKeyHeader == "fake_test_service_role_key_never_leaked");

        var signed = await service.CreateSignedUrlAsync(result.StoragePath, 120);
        Assert.NotNull(signed);
        Assert.True(signed!.IsExternalSignedUrl);
        Assert.Contains("token=shortlived", signed.Url);

        var downloaded = await service.DownloadAsync(result.StoragePath);
        Assert.NotNull(downloaded);
        await using (downloaded!.Value.stream)
        {
            using var ms = new MemoryStream();
            await downloaded.Value.stream.CopyToAsync(ms);
            Assert.Equal(payload, ms.ToArray());
        }

        // Simulation d'indisponibilité Supabase Storage (HTTP 503) -> HttpRequestException remontée
        var failingHandler = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var failingService = new SupabaseFileStorageService(new HttpClient(failingHandler), options, NullLogger<SupabaseFileStorageService>.Instance);

        await using var stream2 = new MemoryStream(payload);
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            failingService.UploadAsync(stream2, "notice.pdf", "application/pdf", societeId: 3));
    }

    [Fact]
    public async Task DocumentController_Upload_StorageOutage_Returns503_And_SignedUrlIsTenantIsolated()
    {
        var dbOpts = new DbContextOptionsBuilder<GmaoDbContext>()
            .UseInMemoryDatabase($"DocCtrlTest_{Guid.NewGuid()}")
            .Options;

        var adminTenant = new Mock<ITenantContextService>();
        adminTenant.Setup(t => t.IsSuperAdmin).Returns(true);
        adminTenant.Setup(t => t.GetRequiredTenantId()).Returns(1);

        int eq1Id, eq2Id;
        await using (var seed = new GmaoDbContext(dbOpts, adminTenant.Object))
        {
            var eq1 = new Equipement { SocieteId = 1, Code = "EQ-1", Designation = "Broyeur 1" };
            var eq2 = new Equipement { SocieteId = 2, Code = "EQ-2", Designation = "Broyeur 2" };
            seed.Equipements.AddRange(eq1, eq2);
            await seed.SaveChangesAsync();
            eq1Id = eq1.Id;
            eq2Id = eq2.Id;

            seed.Documents.Add(new Document
            {
                Nom = "Secret_T2.pdf",
                Chemin = "tenants/2/documents/secret.pdf",
                SocieteId = 2,
                StorageProvider = "Supabase",
                BucketName = "gmao-documents",
                EquipementId = eq2Id,
                UploadedById = 20
            });
            await seed.SaveChangesAsync();
        }

        // User Tenant 1 with Permission
        var user1 = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim("SocieteId", "1"),
            new Claim(ClaimTypes.Role, "Administrateur"),
            new Claim("Permission", "EQUIPMENT_UPDATE")
        }, "TestAuth"));

        var t1Mock = new Mock<ITenantContextService>();
        t1Mock.Setup(t => t.CurrentSocieteId).Returns(1);
        t1Mock.Setup(t => t.GetRequiredTenantId()).Returns(1);

        // 1. Outage => 503 and 0 rows added
        var storageOutageMock = new Mock<IFileStorageService>();
        storageOutageMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Supabase Storage unreachable"));

        await using (var db = new GmaoDbContext(dbOpts, t1Mock.Object))
        {
            var ctrl = new DocumentController(db, storageOutageMock.Object, t1Mock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user1 } }
            };

            var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\nValidContent");
            var formFile = new FormFile(new MemoryStream(pdfBytes), 0, pdfBytes.Length, "file", "test.pdf")
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/pdf"
            };

            var res = await ctrl.Upload(formFile, "notice", "Notice", eq1Id, null, null);
            var objRes = Assert.IsType<ObjectResult>(res);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objRes.StatusCode);
            Assert.Empty(db.Documents.Where(d => d.SocieteId == 1).ToList());
        }

        // 2. Signed URL cross-tenant => 403 Forbid
        var stubStorage = new InMemoryBlobStorageStub();
        await using (var db = new GmaoDbContext(dbOpts, t1Mock.Object))
        {
            var doc2 = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.SocieteId == 2);
            var ctrl = new DocumentController(db, stubStorage, t1Mock.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user1 } }
            };

            var signedCrossRes = await ctrl.GetSignedUrl(doc2.Id);
            Assert.IsType<ForbidResult>(signedCrossRes);
        }
    }

    [Fact]
    public async Task LegacyMigrationRunner_DryRun_Execute_WithSha256Integrity_PreservesLocalFiles_And_Rollback()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"gmao_mig_test_{Guid.NewGuid():N}");
        var docsDir = Path.Combine(tempRoot, "uploads", "documents");
        Directory.CreateDirectory(docsDir);

        try
        {
            var file1Path = Path.Combine(docsDir, "manual1.pdf");
            var file1Bytes = Encoding.ASCII.GetBytes("%PDF-1.5\nLegacy document body\n%%EOF");
            await File.WriteAllBytesAsync(file1Path, file1Bytes);
            var expectedSha = Convert.ToHexString(SHA256.HashData(file1Bytes)).ToLowerInvariant();

            var orphanPath = Path.Combine(docsDir, "orphan.txt");
            await File.WriteAllTextAsync(orphanPath, "untracked");

            var dbOpts = new DbContextOptionsBuilder<GmaoDbContext>()
                .UseInMemoryDatabase($"MigTest_{Guid.NewGuid()}")
                .Options;

            var sysTenant = new SystemMigrationTenantContext();
            int docValidId, docMissingId;

            await using (var seedDb = new GmaoDbContext(dbOpts, sysTenant))
            {
                var eq = new Equipement { SocieteId = 4, Code = "EQ-4", Designation = "Evaporateur" };
                seedDb.Equipements.Add(eq);
                await seedDb.SaveChangesAsync();

                var validDoc = new Document
                {
                    Nom = "manual1.pdf",
                    Chemin = "uploads/documents/manual1.pdf",
                    StorageProvider = "Local",
                    MimeType = "application/pdf",
                    TailleFichierOctets = file1Bytes.Length,
                    EquipementId = eq.Id,
                    UploadedById = 1
                };
                var missingDoc = new Document
                {
                    Nom = "disparu.pdf",
                    Chemin = "uploads/documents/missing.pdf",
                    StorageProvider = "Local",
                    SocieteId = 4,
                    UploadedById = 1
                };

                seedDb.Documents.AddRange(validDoc, missingDoc);
                await seedDb.SaveChangesAsync();
                docValidId = validDoc.Id;
                docMissingId = missingDoc.Id;
            }

            var storageStub = new InMemoryBlobStorageStub();

            // 1. Dry-Run ne modifie rien
            await using (var db = new GmaoDbContext(dbOpts, sysTenant))
            {
                var runner = new LegacyDocumentMigrationRunner(db, storageStub, NullLogger<LegacyDocumentMigrationRunner>.Instance);
                var dryReport = await runner.RunAsync(new LegacyMigrationOptions { Execute = false, WebRootPath = tempRoot });

                Assert.Equal("DryRun", dryReport.Mode);
                Assert.Equal(1, dryReport.ReadyToMigrateCount);
                Assert.Equal(1, dryReport.MissingPhysicalFileCount);
                Assert.Equal(0, dryReport.MigratedCount);
                Assert.Contains(dryReport.OrphanPhysicalFiles, f => f.EndsWith("orphan.txt"));
                Assert.Empty(storageStub.Blobs);

                var dCheck = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == docValidId);
                Assert.Equal("Local", dCheck.StorageProvider);
                Assert.Null(dCheck.SocieteId);
            }

            // 2. Execute avec corruption round-trip -> doit annuler DB & nettoyer blob
            storageStub.CorruptNextDownload = true;
            await using (var db = new GmaoDbContext(dbOpts, sysTenant))
            {
                var runner = new LegacyDocumentMigrationRunner(db, storageStub, NullLogger<LegacyDocumentMigrationRunner>.Instance);
                var corruptedReport = await runner.RunAsync(new LegacyMigrationOptions { Execute = true, WebRootPath = tempRoot });

                Assert.Equal(1, corruptedReport.ErrorCount);
                Assert.Equal(0, corruptedReport.MigratedCount);
                Assert.Empty(storageStub.Blobs); // Blob corrompu nettoyé !
                var dCheck = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == docValidId);
                Assert.Equal("Local", dCheck.StorageProvider);
            }

            // 3. Execute nominal -> vérifie SHA-256 round-trip, met à jour DB, conserve le fichier local, génère manifeste
            storageStub.CorruptNextDownload = false;
            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            await using (var db = new GmaoDbContext(dbOpts, sysTenant))
            {
                var runner = new LegacyDocumentMigrationRunner(db, storageStub, NullLogger<LegacyDocumentMigrationRunner>.Instance);
                var execReport = await runner.RunAsync(new LegacyMigrationOptions
                {
                    Execute = true,
                    WebRootPath = tempRoot,
                    ManifestOutputPath = manifestPath
                });

                Assert.Equal(0, execReport.ErrorCount);
                Assert.Equal(1, execReport.MigratedCount);
                Assert.True(File.Exists(file1Path)); // Ancien fichier local STRICTEMENT conservé !
                Assert.True(File.Exists(manifestPath));

                var dCheck = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == docValidId);
                Assert.Equal("Supabase", dCheck.StorageProvider);
                Assert.Equal("gmao-documents", dCheck.BucketName);
                Assert.Equal(4, dCheck.SocieteId);
                Assert.Equal(expectedSha, dCheck.Sha256Checksum);
                Assert.StartsWith("tenants/4/documents/", dCheck.Chemin);
                Assert.True(storageStub.Blobs.ContainsKey(dCheck.Chemin));
            }

            // 4. Rollback métadonnées depuis manifeste -> restaure état Local d'origine
            await using (var db = new GmaoDbContext(dbOpts, sysTenant))
            {
                var runner = new LegacyDocumentMigrationRunner(db, storageStub, NullLogger<LegacyDocumentMigrationRunner>.Instance);
                var (restored, skipped) = await runner.RollbackFromManifestAsync(manifestPath, tempRoot);

                Assert.Equal(1, restored);
                Assert.Equal(0, skipped);
                var dRestored = await db.Documents.IgnoreQueryFilters().FirstAsync(d => d.Id == docValidId);
                Assert.Equal("Local", dRestored.StorageProvider);
                Assert.Equal("uploads/documents/manual1.pdf", dRestored.Chemin);
                Assert.Null(dRestored.BucketName);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
