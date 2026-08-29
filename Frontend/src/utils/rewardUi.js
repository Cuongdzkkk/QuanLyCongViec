export function validateRewardSeasonForm(form) {
  const errors = []
  if (!form?.name?.trim()) errors.push('Season name is required.')
  if (!['Sprint', 'Month', 'EntireProject', 'Custom'].includes(form?.type)) errors.push('Choose a valid season type.')
  if (!form?.startAt) errors.push('Season start is required.')
  if (form?.type === 'Custom' && form?.endAt && form.endAt <= form.startAt) errors.push('Season end must be after start.')
  return errors
}

export function validateRewardForm(form) {
  const errors = []
  if (!form?.seasonId) errors.push('Choose a season.')
  if (!form?.name?.trim()) errors.push('Reward name is required.')
  if (!['Cash', 'Voucher', 'Gift', 'Privilege', 'Custom'].includes(form?.rewardType)) errors.push('Choose a valid reward type.')
  if (!['TopN', 'SeasonPoints', 'OnTimeRate', 'ApprovedTasks', 'TeamOnTimeRate'].includes(form?.condition)) errors.push('Choose a valid condition.')
  const threshold = Number(form?.threshold)
  if (!Number.isFinite(threshold) || threshold < 0) errors.push('Threshold must be a non-negative number.')
  if (form?.condition === 'TopN' && (!Number.isInteger(Number(form?.rankTo)) || Number(form.rankTo) < 1)) errors.push('Top N must be a positive whole number.')
  return errors
}
