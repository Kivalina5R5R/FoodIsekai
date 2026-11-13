using System.Collections.Generic;
using FoodIsekaiZ.Minigames.Views;
using UnityEngine;

namespace FoodIsekaiZ.Minigames.Games
{
    // Fry It Golden (design 4.1, after Deep Fryer): everyone steps into the pan on the floor, then
    // steps out when THEIR piece on the wall (ringed in their colour) turns golden. Each piece cooks
    // at its own pace, so watch your own. Judged on stepping out: under 0.6 raw, 0.6-0.8 golden,
    // over 0.8 burnt; still in the pan at 1.08 is charred; never in the pan is a miss.
    // Passes when everyone is golden. The look is the Fry prefab (FryView); "in the pan" means
    // standing on the oil inside the floor pan picture, as marked by the prefab's pan zone.
    public sealed class FryGame : Minigame
    {
        private sealed class Piece
        {
            public float Cooked;
            public float Rate;
            public bool InPan;
            public bool EverIn;
            public bool Judged;
            public FryPieceSlot Slot;
        }

        private readonly Dictionary<MinigamePlayer, Piece> pieces = new Dictionary<MinigamePlayer, Piece>();
        private FryView view;
        private bool frying;

        public override string EnglishCommand => "FRY!";
        public override string ThaiCommand => "ทอด!";
        public override string EnglishTitle => "FRY IT GOLDEN";
        public override string ThaiTitle => "ทอดให้เหลือง!";
        public override string EnglishRule => "Step into the pan, step out when YOUR piece is golden.";
        public override string ThaiRule => "ลง​กระทะ แล้ว​ก้าว​ออก​ตอน​ชิ้น​ของ​คุณ​เป็น​สีทอง";

        private float EnterSeconds => 2.1f / Mathf.Sqrt(Speed);

        protected override void Build()
        {
            view = Stage.LoadView<FryView>("Fry", WallLayer, FloorLayer);
            view.SetStatus(Pick("Into the pan!", "ลง​กระทะ​เลย!"));
            view.SetGuideWords(Pick("Step out at", "ก้าว​ออก​ตอน"), Pick("Raw", "ดิบ"), Pick("Golden!", "สีทอง!"), Pick("Burnt", "ไหม้"));

            for (int i = 0; i < Players.Count; i++)
            {
                MinigamePlayer player = Players[i];
                FryPieceSlot slot = view.AddPiece(i, Players.Count, player);
                pieces[player] = new Piece { Rate = Random.Range(0.82f, 1.2f) / (3.6f / Speed), Slot = slot };
            }
        }

        protected override void Step(float deltaTime)
        {
            foreach (MinigamePlayer player in Players)
            {
                Piece piece = pieces[player];
                piece.InPan = player.IsPresent && MinigameZones.InEllipse(player.Position, view.PanCentre, view.PanRadii, piece.InPan);
                piece.EverIn |= piece.InPan;
            }

            if (!frying)
            {
                if (Elapsed < EnterSeconds) return;
                frying = true;
                view.SetStatus(Pick("Step out when yours is golden!", "ก้าว​ออก​ตอน​ชิ้น​ของ​คุณ​เป็น​สีทอง!"));
                foreach (MinigamePlayer player in Players)
                {
                    if (pieces[player].InPan) continue;
                    pieces[player].Judged = true;
                    SetVerdict(player, false, "Missed the pan", "ไม่​ได้​ลง​กระทะ");
                }
            }

            bool waiting = false;
            foreach (MinigamePlayer player in Players)
            {
                Piece piece = pieces[player];
                if (piece.Judged) continue;
                piece.Cooked += piece.Rate * deltaTime;
                piece.Slot.ShowCooked(piece.Cooked);
                if (!piece.InPan) Judge(player, piece);
                else if (piece.Cooked >= FryCooking.CharredAt)
                {
                    piece.Judged = true;
                    SetVerdict(player, false, "Charred!", "ไหม้​เกรียม!");
                }
                else waiting = true;
            }

            if (!waiting) FinishGame();
        }

        protected override void OnTimeUp()
        {
            foreach (MinigamePlayer player in Players)
            {
                Piece piece = pieces[player];
                if (piece.Judged) continue;
                if (piece.EverIn) Judge(player, piece);
                else SetVerdict(player, false, "Missed the pan", "ไม่​ได้​ลง​กระทะ");
            }

            FinishGame();
        }

        private void Judge(MinigamePlayer player, Piece piece)
        {
            piece.Judged = true;
            if (piece.Cooked < FryCooking.GoldenFrom) SetVerdict(player, false, "Still raw", "ยัง​ดิบ");
            else if (piece.Cooked <= FryCooking.BurntFrom) SetVerdict(player, true, "Golden!", "เหลือง​พอดี!");
            else SetVerdict(player, false, "Burnt", "ไหม้");
        }

        private void FinishGame()
        {
            Finish(Players.Count > 0 && SuccessCount() == Players.Count);
        }
    }
}
