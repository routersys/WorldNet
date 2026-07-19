namespace WorldNet;

public static unsafe partial class Harvest
{
    private static double SelectBestF0(double referenceF0, double* f0Candidates,
        int numberOfCandidates, double allowedRange, double* bestError)
    {
        double bestF0 = 0.0;
        *bestError = allowedRange;

        for (int i = 0; i < numberOfCandidates; ++i)
        {
            double tmp = Math.Abs(referenceF0 - f0Candidates[i]) / referenceF0;
            if (tmp > *bestError)
            {
                continue;
            }
            bestF0 = f0Candidates[i];
            *bestError = tmp;
        }

        return bestF0;
    }

    private static void RemoveUnreliableCandidatesSub(int i, int j, double** tmpF0Candidates,
        int numberOfCandidates, double** f0Candidates, double** f0Scores)
    {
        double referenceF0 = f0Candidates[i][j];
        double threshold = 0.05;
        if (referenceF0 == 0)
        {
            return;
        }
        double error1 = 0.0;
        double error2 = 0.0;
        SelectBestF0(referenceF0, tmpF0Candidates[i + 1], numberOfCandidates, 1.0, &error1);
        SelectBestF0(referenceF0, tmpF0Candidates[i - 1], numberOfCandidates, 1.0, &error2);
        double minError = WorldMath.MinDouble(error1, error2);
        if (minError <= threshold)
        {
            return;
        }
        f0Candidates[i][j] = 0;
        f0Scores[i][j] = 0;
    }

    private static void RemoveUnreliableCandidates(int f0Length, int numberOfCandidates,
        double** f0Candidates, double** f0Scores, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        double** tmpF0Candidates = (double**)arena.AllocateRaw(f0Length, (nuint)sizeof(double*));
        double* tmpStorage = (double*)arena.AllocateRaw(f0Length * numberOfCandidates, sizeof(double));
        for (int i = 0; i < f0Length; ++i)
        {
            tmpF0Candidates[i] = tmpStorage + (i * numberOfCandidates);
        }
        for (int i = 0; i < f0Length; ++i)
        {
            for (int j = 0; j < numberOfCandidates; ++j)
            {
                tmpF0Candidates[i][j] = f0Candidates[i][j];
            }
        }

        for (int i = 1; i < f0Length - 1; ++i)
        {
            for (int j = 0; j < numberOfCandidates; ++j)
            {
                RemoveUnreliableCandidatesSub(i, j, tmpF0Candidates, numberOfCandidates,
                    f0Candidates, f0Scores);
            }
        }
    }

    private static void SearchF0Base(double** f0Candidates, double** f0Scores, int f0Length,
        int numberOfCandidates, double* baseF0Contour)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            double tmpBestScore = 0.0;
            baseF0Contour[i] = 0.0;
            for (int j = 0; j < numberOfCandidates; ++j)
            {
                if (f0Scores[i][j] > tmpBestScore)
                {
                    baseF0Contour[i] = f0Candidates[i][j];
                    tmpBestScore = f0Scores[i][j];
                }
            }
        }
    }

    private static void FixStep1(double* f0Base, int f0Length, double allowedRange,
        double* f0Step1)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step1[i] = 0.0;
        }
        for (int i = 2; i < f0Length; ++i)
        {
            if (f0Base[i] == 0.0)
            {
                continue;
            }
            double referenceF0 = (f0Base[i - 1] * 2) - f0Base[i - 2];
            f0Step1[i] =
                Math.Abs((f0Base[i] - referenceF0) / referenceF0) > allowedRange &&
                Math.Abs(f0Base[i] - f0Base[i - 1]) / f0Base[i - 1] > allowedRange ?
                0.0 : f0Base[i];
        }
    }

    private static int GetBoundaryList(double* f0, int f0Length, int* boundaryList,
        WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        int numberOfBoundaries = 0;
        int* vuv = (int*)arena.AllocateRaw(f0Length, sizeof(int));
        for (int i = 0; i < f0Length; ++i)
        {
            vuv[i] = f0[i] > 0 ? 1 : 0;
        }
        vuv[0] = vuv[f0Length - 1] = 0;

        for (int i = 1; i < f0Length; ++i)
        {
            if (vuv[i] - vuv[i - 1] != 0)
            {
                boundaryList[numberOfBoundaries] = i - (numberOfBoundaries % 2);
                numberOfBoundaries++;
            }
        }

        return numberOfBoundaries;
    }

    private static void FixStep2(double* f0Step1, int f0Length, int voiceRangeMinimum,
        double* f0Step2, WorldArena arena)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step2[i] = f0Step1[i];
        }
        using WorldArenaScope scope = arena.BeginScope();
        int* boundaryList = (int*)arena.AllocateRaw(f0Length, sizeof(int));
        int numberOfBoundaries = GetBoundaryList(f0Step1, f0Length, boundaryList, arena);

        for (int i = 0; i < numberOfBoundaries / 2; ++i)
        {
            if (boundaryList[(i * 2) + 1] - boundaryList[i * 2] >= voiceRangeMinimum)
            {
                continue;
            }
            for (int j = boundaryList[i * 2]; j <= boundaryList[(i * 2) + 1]; ++j)
            {
                f0Step2[j] = 0.0;
            }
        }
    }

    private static void GetMultiChannelF0(double* f0, int f0Length, int* boundaryList,
        int numberOfBoundaries, double** multiChannelF0)
    {
        for (int i = 0; i < numberOfBoundaries / 2; ++i)
        {
            for (int j = 0; j < boundaryList[i * 2]; ++j)
            {
                multiChannelF0[i][j] = 0.0;
            }
            for (int j = boundaryList[i * 2]; j <= boundaryList[(i * 2) + 1]; ++j)
            {
                multiChannelF0[i][j] = f0[j];
            }
            for (int j = boundaryList[(i * 2) + 1] + 1; j < f0Length; ++j)
            {
                multiChannelF0[i][j] = 0.0;
            }
        }
    }

    private static int MyAbsInt(int x)
    {
        return x > 0 ? x : -x;
    }

    private static int ExtendF0(int origin, int lastPoint, int shift, double** f0Candidates,
        int numberOfCandidates, double allowedRange, double* extendedF0, WorldArena arena)
    {
        int threshold = 4;
        double tmpF0 = extendedF0[origin];
        int shiftedOrigin = origin;

        int distance = MyAbsInt(lastPoint - origin);
        using WorldArenaScope scope = arena.BeginScope();
        int* indexList = (int*)arena.AllocateRaw(distance + 1, sizeof(int));
        for (int i = 0; i <= distance; ++i)
        {
            indexList[i] = origin + (shift * i);
        }

        int count = 0;
        double dammy = 0.0;
        for (int i = 0; i <= distance; ++i)
        {
            extendedF0[indexList[i] + shift] =
                SelectBestF0(tmpF0, f0Candidates[indexList[i] + shift], numberOfCandidates,
                    allowedRange, &dammy);
            if (extendedF0[indexList[i] + shift] == 0.0)
            {
                count++;
            }
            else
            {
                tmpF0 = extendedF0[indexList[i] + shift];
                count = 0;
                shiftedOrigin = indexList[i] + shift;
            }
            if (count == threshold)
            {
                break;
            }
        }

        return shiftedOrigin;
    }

    private static void Swap(int index1, int index2, double** f0, int* boundary)
    {
        double* tmpPointer = f0[index1];
        f0[index1] = f0[index2];
        f0[index2] = tmpPointer;
        int tmpIndex = boundary[index1 * 2];
        boundary[index1 * 2] = boundary[index2 * 2];
        boundary[index2 * 2] = tmpIndex;
        tmpIndex = boundary[(index1 * 2) + 1];
        boundary[(index1 * 2) + 1] = boundary[(index2 * 2) + 1];
        boundary[(index2 * 2) + 1] = tmpIndex;
    }

    private static int ExtendSub(double** extendedF0, int* boundaryList, int numberOfSections,
        double** selectedExtendedF0, int* selectedBoundaryList)
    {
        double threshold = 2200.0;
        int count = 0;
        double meanF0 = 0.0;
        for (int i = 0; i < numberOfSections; ++i)
        {
            int st = boundaryList[i * 2];
            int ed = boundaryList[(i * 2) + 1];
            for (int j = st; j < ed; ++j)
            {
                meanF0 += extendedF0[i][j];
            }
            meanF0 /= ed - st;
            if (threshold / meanF0 < ed - st)
            {
                Swap(count++, i, selectedExtendedF0, selectedBoundaryList);
            }
        }
        return count;
    }

    private static int Extend(double** multiChannelF0, int numberOfSections, int f0Length,
        int* boundaryList, double** f0Candidates, int numberOfCandidates, double allowedRange,
        double** extendedF0, int* shiftedBoundaryList, WorldArena arena)
    {
        int threshold = 100;
        for (int i = 0; i < numberOfSections; ++i)
        {
            shiftedBoundaryList[(i * 2) + 1] = ExtendF0(boundaryList[(i * 2) + 1],
                WorldMath.MinInt(f0Length - 2, boundaryList[(i * 2) + 1] + threshold), 1,
                f0Candidates, numberOfCandidates, allowedRange, extendedF0[i], arena);
            shiftedBoundaryList[i * 2] = ExtendF0(boundaryList[i * 2],
                WorldMath.MaxInt(1, boundaryList[i * 2] - threshold), -1,
                f0Candidates, numberOfCandidates, allowedRange, extendedF0[i], arena);
        }

        return ExtendSub(multiChannelF0, shiftedBoundaryList, numberOfSections, extendedF0,
            shiftedBoundaryList);
    }

    private static void MakeSortedOrder(int* boundaryList, int numberOfSections, int* order)
    {
        for (int i = 0; i < numberOfSections; ++i)
        {
            order[i] = i;
        }
        for (int i = 1; i < numberOfSections; ++i)
        {
            for (int j = i - 1; j >= 0; --j)
            {
                if (boundaryList[order[j] * 2] > boundaryList[order[i] * 2])
                {
                    int tmp = order[i];
                    order[i] = order[j];
                    order[j] = tmp;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private static double SearchScore(double f0, double* f0Candidates, double* f0Scores,
        int numberOfCandidates)
    {
        double score = 0.0;
        for (int i = 0; i < numberOfCandidates; ++i)
        {
            if (f0 == f0Candidates[i] && score < f0Scores[i])
            {
                score = f0Scores[i];
            }
        }
        return score;
    }

    private static int MergeF0Sub(double* f0_1, int f0Length, int st1, int ed1, double* f0_2,
        int st2, int ed2, double** f0Candidates, double** f0Scores, int numberOfCandidates,
        double* mergedF0)
    {
        if (st1 <= st2 && ed1 >= ed2)
        {
            return ed1;
        }

        double score1 = 0.0;
        double score2 = 0.0;
        for (int i = st2; i <= ed1; ++i)
        {
            score1 += SearchScore(f0_1[i], f0Candidates[i], f0Scores[i], numberOfCandidates);
            score2 += SearchScore(f0_2[i], f0Candidates[i], f0Scores[i], numberOfCandidates);
        }
        if (score1 > score2)
        {
            for (int i = ed1; i <= ed2; ++i)
            {
                mergedF0[i] = f0_2[i];
            }
        }
        else
        {
            for (int i = st2; i <= ed2; ++i)
            {
                mergedF0[i] = f0_2[i];
            }
        }

        return ed2;
    }

    private static void MergeF0(double** multiChannelF0, int* boundaryList, int numberOfChannels,
        int f0Length, double** f0Candidates, double** f0Scores, int numberOfCandidates,
        double* mergedF0, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        int* order = (int*)arena.AllocateRaw(numberOfChannels, sizeof(int));
        MakeSortedOrder(boundaryList, numberOfChannels, order);

        for (int i = 0; i < f0Length; ++i)
        {
            mergedF0[i] = multiChannelF0[0][i];
        }

        for (int i = 1; i < numberOfChannels; ++i)
        {
            if (boundaryList[order[i] * 2] - boundaryList[1] > 0)
            {
                for (int j = boundaryList[order[i] * 2]; j <= boundaryList[(order[i] * 2) + 1]; ++j)
                {
                    mergedF0[j] = multiChannelF0[order[i]][j];
                }
                boundaryList[0] = boundaryList[order[i] * 2];
                boundaryList[1] = boundaryList[(order[i] * 2) + 1];
            }
            else
            {
                boundaryList[1] =
                    MergeF0Sub(mergedF0, f0Length, boundaryList[0], boundaryList[1],
                    multiChannelF0[order[i]], boundaryList[order[i] * 2],
                    boundaryList[(order[i] * 2) + 1], f0Candidates, f0Scores,
                    numberOfCandidates, mergedF0);
            }
        }
    }

    private static void FixStep3(double* f0Step2, int f0Length, int numberOfCandidates,
        double** f0Candidates, double allowedRange, double** f0Scores, double* f0Step3,
        WorldArena arena)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step3[i] = f0Step2[i];
        }
        using WorldArenaScope scope = arena.BeginScope();
        int* boundaryList = (int*)arena.AllocateRaw(f0Length, sizeof(int));
        int numberOfBoundaries = GetBoundaryList(f0Step2, f0Length, boundaryList, arena);

        int sections = numberOfBoundaries / 2;
        double** multiChannelF0 = (double**)arena.AllocateRaw(sections, (nuint)sizeof(double*));
        double* storage = (double*)arena.AllocateRaw(sections * f0Length, sizeof(double));
        for (int i = 0; i < sections; ++i)
        {
            multiChannelF0[i] = storage + (i * f0Length);
        }
        GetMultiChannelF0(f0Step2, f0Length, boundaryList, numberOfBoundaries, multiChannelF0);

        int numberOfChannels =
            Extend(multiChannelF0, sections, f0Length, boundaryList, f0Candidates,
            numberOfCandidates, allowedRange, multiChannelF0, boundaryList, arena);

        if (numberOfChannels != 0)
        {
            MergeF0(multiChannelF0, boundaryList, numberOfChannels, f0Length, f0Candidates,
                f0Scores, numberOfCandidates, f0Step3, arena);
        }
    }

    private static void FixStep4(double* f0Step3, int f0Length, int threshold, double* f0Step4,
        WorldArena arena)
    {
        for (int i = 0; i < f0Length; ++i)
        {
            f0Step4[i] = f0Step3[i];
        }
        using WorldArenaScope scope = arena.BeginScope();
        int* boundaryList = (int*)arena.AllocateRaw(f0Length, sizeof(int));
        int numberOfBoundaries = GetBoundaryList(f0Step3, f0Length, boundaryList, arena);

        for (int i = 0; i < (numberOfBoundaries / 2) - 1; ++i)
        {
            int distance = boundaryList[(i + 1) * 2] - boundaryList[(i * 2) + 1] - 1;
            if (distance >= threshold)
            {
                continue;
            }
            double tmp0 = f0Step3[boundaryList[(i * 2) + 1]] + 1;
            double tmp1 = f0Step3[boundaryList[(i + 1) * 2]] - 1;
            double coefficient = (tmp1 - tmp0) / (distance + 1.0);
            int count = 1;
            for (int j = boundaryList[(i * 2) + 1] + 1; j <= boundaryList[(i + 1) * 2] - 1; ++j)
            {
                f0Step4[j] = tmp0 + (coefficient * count++);
            }
        }
    }

    private static void FixF0Contour(double** f0Candidates, double** f0Scores, int f0Length,
        int numberOfCandidates, double* bestF0Contour, WorldArena arena)
    {
        using WorldArenaScope scope = arena.BeginScope();
        double* tmpF0Contour1 = (double*)arena.AllocateRaw(f0Length, sizeof(double));
        double* tmpF0Contour2 = (double*)arena.AllocateRaw(f0Length, sizeof(double));

        SearchF0Base(f0Candidates, f0Scores, f0Length, numberOfCandidates, tmpF0Contour1);
        FixStep1(tmpF0Contour1, f0Length, 0.008, tmpF0Contour2);
        FixStep2(tmpF0Contour2, f0Length, 6, tmpF0Contour1, arena);
        FixStep3(tmpF0Contour1, f0Length, numberOfCandidates, f0Candidates, 0.18, f0Scores,
            tmpF0Contour2, arena);
        FixStep4(tmpF0Contour2, f0Length, 9, bestF0Contour, arena);
    }

    private static void FilteringF0(double* a, double* b, double* x, int xLength, int st, int ed,
        double* y, WorldArena arena)
    {
        double w0 = 0.0;
        double w1 = 0.0;
        using WorldArenaScope scope = arena.BeginScope();
        double* tmpX = (double*)arena.AllocateRaw(xLength, sizeof(double));

        for (int i = 0; i < st; ++i)
        {
            x[i] = x[st];
        }
        for (int i = ed + 1; i < xLength; ++i)
        {
            x[i] = x[ed];
        }

        for (int i = 0; i < xLength; ++i)
        {
            double wt = x[i] + (a[0] * w0) + (a[1] * w1);
            tmpX[xLength - i - 1] = (b[0] * wt) + (b[1] * w0) + (b[0] * w1);
            w1 = w0;
            w0 = wt;
        }

        w0 = w1 = 0.0;
        for (int i = 0; i < xLength; ++i)
        {
            double wt = tmpX[i] + (a[0] * w0) + (a[1] * w1);
            y[xLength - i - 1] = (b[0] * wt) + (b[1] * w0) + (b[0] * w1);
            w1 = w0;
            w0 = wt;
        }
    }

    private static void SmoothF0Contour(double* f0, int f0Length, double* smoothedF0,
        WorldArena arena)
    {
        double* b = stackalloc double[2] { 0.0078202080334971724, 0.015640416066994345 };
        double* a = stackalloc double[2] { 1.7347257688092754, -0.76600660094326412 };
        int lag = 300;
        int newF0Length = f0Length + (lag * 2);
        using WorldArenaScope scope = arena.BeginScope();
        double* f0Contour = (double*)arena.AllocateRaw(newF0Length, sizeof(double));
        for (int i = 0; i < lag; ++i)
        {
            f0Contour[i] = 0.0;
        }
        for (int i = lag; i < lag + f0Length; ++i)
        {
            f0Contour[i] = f0[i - lag];
        }
        for (int i = lag + f0Length; i < newF0Length; ++i)
        {
            f0Contour[i] = 0.0;
        }

        int* boundaryList = (int*)arena.AllocateRaw(newF0Length, sizeof(int));
        int numberOfBoundaries = GetBoundaryList(f0Contour, newF0Length, boundaryList, arena);
        int sections = numberOfBoundaries / 2;
        double** multiChannelF0 = (double**)arena.AllocateRaw(sections, (nuint)sizeof(double*));
        double* storage = (double*)arena.AllocateRaw(sections * newF0Length, sizeof(double));
        for (int i = 0; i < sections; ++i)
        {
            multiChannelF0[i] = storage + (i * newF0Length);
        }
        GetMultiChannelF0(f0Contour, newF0Length, boundaryList, numberOfBoundaries,
            multiChannelF0);

        for (int i = 0; i < sections; ++i)
        {
            FilteringF0(a, b, multiChannelF0[i], newF0Length, boundaryList[i * 2],
                boundaryList[(i * 2) + 1], f0Contour, arena);
            for (int j = boundaryList[i * 2]; j <= boundaryList[(i * 2) + 1]; ++j)
            {
                smoothedF0[j - lag] = f0Contour[j];
            }
        }
    }
}
