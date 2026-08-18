CREATE TABLE IF NOT EXISTS public."OrderItemAnswers"
(
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderItemId" UUID NOT NULL REFERENCES public."OrderItems"("Id") ON DELETE CASCADE,
    "ProjectId" UUID NOT NULL,
    "QuestionId" UUID NOT NULL,
    "ItemId"  UUID NULL REFERENCES public."ProjectItems"("Id"),
    "Answer" TEXT NOT NULL DEFAULT '',
    "Created" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "Updated" TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS "IX_OrderItemAnswers_OrderItemId" ON public."OrderItemAnswers" ("OrderItemId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrderItemAnswers_OrderItemId_QuestionId" ON public."OrderItemAnswers" ("OrderItemId", "QuestionId");
