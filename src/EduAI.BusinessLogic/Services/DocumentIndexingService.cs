using System.Text.Json;
using EduAI.BusinessLogic.Helpers;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;

namespace EduAI.BusinessLogic.Services;

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiAiService _geminiAiService;
    private readonly INotificationService _notificationService;

    public DocumentIndexingService(
        IUnitOfWork unitOfWork,
        IGeminiAiService geminiAiService,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _geminiAiService = geminiAiService;
        _notificationService = notificationService;
    }

    public async Task IndexAsync(int documentId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(documentId);
        if (document == null)
            return;

        if (!File.Exists(document.FilePath))
        {
            await NotifyProgressAsync(documentId, "Failed", new { error = "File is missing on server." });
            return;
        }

        await NotifyProgressAsync(documentId, "Started", new { status = "Processing", chunkCount = 0, embedded = 0 });

        int chunkCount;
        await using (var readStream = File.OpenRead(document.FilePath))
        {
            chunkCount = await CreateChunksAsync(document, readStream, cancellationToken);
        }

        await NotifyProgressAsync(documentId, "ChunksCreated", new { status = "Processing", chunkCount, embedded = 0 });

        var createdChunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
        var embedded = 0;
        foreach (var chunk in createdChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedInput = chunk.Content.Length > 8000 ? chunk.Content[..8000] : chunk.Content;
            var vector = await _geminiAiService.EmbedTextAsync(embedInput);
            await _unitOfWork.Embeddings.AddAsync(new DocumentEmbedding
            {
                ChunkId = chunk.Id,
                SubjectId = document.SubjectId,
                ChapterId = document.ChapterId,
                DocumentId = document.Id,
                EmbeddingVector = VectorHelper.Serialize(vector)
            });
            embedded++;

            if (embedded % 5 == 0 || embedded == chunkCount)
                await NotifyProgressAsync(documentId, "EmbeddingProgress", new { status = "Processing", chunkCount, embedded });
        }

        await _unitOfWork.SaveChangesAsync();
        await NotifyProgressAsync(documentId, "Completed", new
        {
            status = "Indexed",
            chunkCount,
            embedded,
            processedAt = DateTime.UtcNow
        });
    }

    private async Task<int> CreateChunksAsync(Document document, Stream fileStream, CancellationToken cancellationToken)
    {
        fileStream.Position = 0;
        var text = await DocumentTextExtractor.ExtractTextAsync(fileStream, document.FileName);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("No readable text was found in the uploaded file.");

        var textChunks = DocumentTextExtractor.ChunkText(text);
        if (textChunks.Count == 0)
            throw new InvalidOperationException("Document text could not be split into chunks.");

        var index = 0;
        foreach (var content in textChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _unitOfWork.Chunks.AddAsync(new DocumentChunk
            {
                SubjectId = document.SubjectId,
                ChapterId = document.ChapterId,
                DocumentId = document.Id,
                Content = content,
                ChunkIndex = index++
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return textChunks.Count;
    }

    private Task NotifyProgressAsync(int documentId, string action, object payload) =>
        _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "DocumentIndex",
            Action = action,
            EntityId = documentId,
            Message = JsonSerializer.Serialize(payload)
        });
}

